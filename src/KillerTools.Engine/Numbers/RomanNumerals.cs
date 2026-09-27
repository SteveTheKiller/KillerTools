using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Numbers;

public static partial class RomanNumerals
{
    private static readonly (string Symbol, int Value)[] Values = [("M",1000),("CM",900),("D",500),("CD",400),("C",100),("XC",90),("L",50),("XL",40),("X",10),("IX",9),("V",5),("IV",4),("I",1)];

    public static string FromArabic(int number)
    {
        if (number is < 1 or > 3999) return string.Empty;
        var output = new StringBuilder();
        foreach (var (symbol, value) in Values)
        {
            while (number >= value) { output.Append(symbol); number -= value; }
        }
        return output.ToString();
    }

    public static int? ToArabic(string roman)
    {
        ArgumentNullException.ThrowIfNull(roman);
        if (!Valid().IsMatch(roman)) return null;
        var map = new Dictionary<char, int> { ['I']=1, ['V']=5, ['X']=10, ['L']=50, ['C']=100, ['D']=500, ['M']=1000 };
        var total = 0;
        for (var index = 0; index < roman.Length; index++) total += index + 1 < roman.Length && map[roman[index + 1]] > map[roman[index]] ? -map[roman[index]] : map[roman[index]];
        return total;
    }

    [GeneratedRegex("^M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})$")]
    private static partial Regex Valid();
}
