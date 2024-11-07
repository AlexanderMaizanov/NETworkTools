using System;
using System.Threading.Tasks;

namespace NETworkManager.Models.Network;

public static class BitCalculator
{
    public static Task<BitCalculatorInfo> CalculateAsync(double input, BitCalculatorUnit unit,
        BitCalculatorNotation notation)
    {
        return Task.Run(() => Calculate(input, unit, notation));
    }

    public static BitCalculatorInfo Calculate(double input, BitCalculatorUnit unit, BitCalculatorNotation notation)
    {
        // Get bits from input
        var u = GetUnitBase(unit);
        var n = GetNotationBase(notation);

        double bits;

        if (unit.ToString().EndsWith("Bits", StringComparison.OrdinalIgnoreCase))
            bits = input * Math.Pow(n, u);
        else
            bits = input * 8 * Math.Pow(n, u);

        // Return caculation
        return new BitCalculatorInfo
        {
            Bits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Bits)),
            Bytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Bytes)),
            Kilobits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Kilobits)),
            Kilobytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Kilobytes)),
            Megabits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Megabits)),
            Megabytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Megabytes)),
            Gigabits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Gigabits)),
            Gigabytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Gigabytes)),
            Terabits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Terabits)),
            Terabytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Terabytes)),
            Petabits = bits / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Petabits)),
            Petabytes = bits / 8 / Math.Pow(n, GetUnitBase(BitCalculatorUnit.Petabytes))
        };
    }

    private static int GetNotationBase(BitCalculatorNotation notation)
    {
        return notation == BitCalculatorNotation.Binary ? 1024 : 1000;
    }

    private static int GetUnitBase(BitCalculatorUnit unit)
    {
        return unit switch
        {
            BitCalculatorUnit.Bits => 0,
            BitCalculatorUnit.Bytes => 0,
            BitCalculatorUnit.Kilobits => 1,
            BitCalculatorUnit.Kilobytes => 1,
            BitCalculatorUnit.Megabits => 2,
            BitCalculatorUnit.Megabytes => 2,
            BitCalculatorUnit.Gigabits => 3,
            BitCalculatorUnit.Gigabytes => 3,
            BitCalculatorUnit.Terabits => 4,
            BitCalculatorUnit.Terabytes => 4,
            BitCalculatorUnit.Petabits => 5,
            BitCalculatorUnit.Petabytes => 5,
            _ => -1
        };
    }
}