# https://stackoverflow.com/questions/33956505/get-pycharm-to-know-what-classes-are-mixin-for

# Or just use diamod inheritance?

class auto_Foo:
    def __init__(self):
        self._a = "a"

    @property
    def a(self) -> str:
        return self._a

    @a.setter
    def a(self, val: str):
        self._a = val


# class auto_Bar:
#     a: str

class auto_Bar(auto_Foo):

    @property
    def b(self) -> str:
        """Docstring goes here."""
        return "b"

    def test(self):
        return self.a + self.b


class Foo(auto_Foo):
    def fofo(self):
        print(f'fofo: {self.a}')


# class Bar(Foo, auto_Bar):
class Bar(auto_Bar, Foo):
    def hej(self):
        self.a = 12
        print(self.a)
        print(self.b)
        self.fofo()



f = Foo()
b = Bar()

print(f.a)
print(b.a)
print(b.b)
print(b.test())

b.hej()
