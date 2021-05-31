import brick
print(brick)
# from brick.Foo import Foo, MyFoo
from brick.DoublePendulum.Link import Link
from brick.DoublePendulum.MyPendulum import MyLink, MyAttachedPendulum
from brick.FlatCrane.Boom import Boom
from brick.FlatCrane.Crane import Crane, LinkCrane
from timeit import default_timer as timer

brick.initLogging(level=brick.logging.INFO)


# f = Foo()
# f = MyFoo()
# print(f"MyFoo: {f}")
# print(f"MyFoo: {f.dict()}")
# f.bar = 24
# print(f"MyFoo: {f.dict()}")


# l = MyLink()

# a = MyAttachedPendulum()

# b = Boom()

t0 = timer()
# c = Crane()
t1 = timer()
# c = Crane(_skipLoad = True)
t2 = timer()
lc = LinkCrane()
t3 = timer()
print(f'first: {t1-t0}')
print(f'second: {t2-t1}')
print(f'lc: {t3-t2}')
