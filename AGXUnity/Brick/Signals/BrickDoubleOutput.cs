public class BrickDoubleOutput : BrickOutput<double, double>
{
  protected override double GetSignalData(double internalData)
  {
    return internalData;
  }
}
