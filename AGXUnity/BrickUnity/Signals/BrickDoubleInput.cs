namespace AGXUnity.BrickUnity.Signals
{
  public class BrickDoubleInput : BrickInput<double, double>
  {
    public override double ConvertData(double data)
    {
      return data;
    }
  }
}