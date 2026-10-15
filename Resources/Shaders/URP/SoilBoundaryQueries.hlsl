// Collision data is a rendering-only cache in Unity world/rigid-local coordinates.
struct SoilBoundaryShape { float4x4 worldToLocal; float4 sizeKind; float4 metadata; };
struct SoilBoundaryNode { float3 minimum; int start; float3 maximum; int count; };
struct SoilBoundaryTriangle { float4 a, b, c; };
struct SoilBoundaryContacts { float4 indices, sides; };
StructuredBuffer<SoilBoundaryShape> _SoilBoundaryShapes;
StructuredBuffer<SoilBoundaryNode> _SoilBoundaryNodes;
StructuredBuffer<SoilBoundaryTriangle> _SoilBoundaryTriangles;
StructuredBuffer<SoilBoundaryTriangle> _SoilBoundaryPatches;
StructuredBuffer<SoilBoundaryContacts> _SoilBoundaryContacts;
float _SoilBoundaryEnabled, _SoilBoundaryClipRange;

float3 SoilClosestTriangle(float3 p, float3 a, float3 b, float3 c)
{
  float3 ab=b-a, ac=c-a, ap=p-a; float d1=dot(ab,ap), d2=dot(ac,ap);
  if (d1<=0 && d2<=0) return a;
  float3 bp=p-b; float d3=dot(ab,bp), d4=dot(ac,bp); if (d3>=0 && d4<=d3) return b;
  float vc=d1*d4-d3*d2; if (vc<=0 && d1>=0 && d3<=0) return a+ab*d1/(d1-d3);
  float3 cp=p-c; float d5=dot(ab,cp), d6=dot(ac,cp); if (d6>=0 && d5<=d6) return c;
  float vb=d5*d2-d1*d6; if (vb<=0 && d2>=0 && d6<=0) return a+ac*d2/(d2-d6);
  float va=d3*d6-d5*d4; if (va<=0 && d4-d3>=0 && d5-d6>=0) return b+(c-b)*(d4-d3)/((d4-d3)+(d5-d6));
  return a+(ab*vb+ac*vc)/(va+vb+vc);
}
float SoilBoundaryBoxDistance(SoilBoundaryNode node, float3 p)
{
  float3 gap=max(0,max(node.minimum-p,p-node.maximum)); return dot(gap,gap);
}
float SoilBoundarySignedDistance(int index, float side, float3 world, float reach)
{
  SoilBoundaryShape shape=_SoilBoundaryShapes[index];
  float3 p=mul(shape.worldToLocal,float4(world,1)).xyz;
  int kind=(int)shape.sizeKind.w;
  if (kind==0) return reach;
  if (kind==1) return p.y*side;
  if (kind==2) {
    float3 q=abs(p)-shape.sizeKind.xyz;
    return length(max(q,0))+min(max(q.x,max(q.y,q.z)),0);
  }
  int root=(int)shape.metadata.x; if (root<0) return reach;
  if (shape.metadata.y>0.5) {
    SoilBoundaryNode bounds=_SoilBoundaryNodes[root];
    // A height tile is a surface patch, not an infinite extension of its edge.
    if (any(p.xz<bounds.minimum.xz-1e-5) || any(p.xz>bounds.maximum.xz+1e-5)) return reach;
    if (shape.metadata.z>0.5) return (dot(p,shape.sizeKind.xyz)-shape.metadata.w)*side;
  }
  float best=reach*reach, signedDistance=reach;
  int stack[32]; int top=0; stack[top++]=root;
  [loop] while(top>0) {
    SoilBoundaryNode node=_SoilBoundaryNodes[stack[--top]];
    if (SoilBoundaryBoxDistance(node,p)>best) continue;
    if (node.count>0) {
      [loop] for(int i=0;i<node.count;++i) {
        SoilBoundaryTriangle face=_SoilBoundaryTriangles[node.start+i];
        float3 q=SoilClosestTriangle(p,face.a.xyz,face.b.xyz,face.c.xyz);
        float3 delta=p-q; float distance=dot(delta,delta);
        if(distance<best) {
          best=distance;
          float3 normal=cross(face.b.xyz-face.a.xyz,face.c.xyz-face.a.xyz);
          signedDistance=dot(delta,normal)*side<0 ? -sqrt(distance) : sqrt(distance);
        }
      }
    }
    else {
      int a=node.start, b=a+1;
      if(SoilBoundaryBoxDistance(_SoilBoundaryNodes[a],p)<SoilBoundaryBoxDistance(_SoilBoundaryNodes[b],p)) { int swap=a;a=b;b=swap; }
      // The balanced BVH has at most one deferred sibling per level; 32
      // entries cover every supported signed 32-bit triangle count.
      stack[top++]=a; stack[top++]=b;
    }
  }
  return signedDistance;
}
bool SoilHasBoundary(uint particle)
{
  if(_SoilBoundaryEnabled<0.5) return false;
  return any(_SoilBoundaryContacts[particle].indices>=0);
}
float SoilBoundaryClearance(float3 world,uint3 particles,int particleCount)
{
  if(_SoilBoundaryEnabled<0.5) return 1;
  float clearance=1; int visited[12]; int visitedCount=0;
  float radius=0;
  [unroll] for(int r=0;r<3;++r) if(r<particleCount) radius=max(radius,_SoilParticles[particles[r]].radius);
  for(int p=0;p<particleCount;++p) {
    SoilBoundaryContacts contacts=_SoilBoundaryContacts[particles[p]];
    [loop] for(int i=0;i<4;++i) {
      int shape=(int)contacts.indices[i]; if(shape<0) continue;
      int key=shape*2+(contacts.sides[i]<0 ? 1 : 0); bool duplicate=false;
      [unroll] for(int j=0;j<12;++j) if(j<visitedCount && visited[j]==key) duplicate=true;
      if(duplicate) continue; visited[visitedCount++]=key;
      clearance=min(clearance,SoilBoundarySignedDistance(shape,contacts.sides[i],world,radius*_SoilBoundaryClipRange)+max(1e-5,radius*0.001));
    }
  }
  return clearance;
}
float SoilBoundaryClearance(float3 world,uint particle)
{
  if(_SoilBoundaryEnabled<0.5) return 1;
  SoilBoundaryContacts contacts=_SoilBoundaryContacts[particle];
  float radius=_SoilParticles[particle].radius, clearance=1;
  [loop] for(int i=0;i<4;++i) {
    int shape=(int)contacts.indices[i]; if(shape<0) continue;
    clearance=min(clearance,SoilBoundarySignedDistance(shape,contacts.sides[i],world,radius*_SoilBoundaryClipRange)+max(1e-5,radius*0.001));
  }
  return clearance;
}
