using System;

namespace UserService.Common;

public static class ZodiacHelper
{
    public static string ComputeZodiacSign(DateTime dateOfBirth)
    {
        int day = dateOfBirth.Day;
        int month = dateOfBirth.Month;

        return month switch
        {
            1 => day <= 19 ? "Capricorn" : "Aquarius",
            2 => day <= 18 ? "Aquarius" : "Pisces",
            3 => day <= 20 ? "Pisces" : "Aries",
            4 => day <= 19 ? "Aries" : "Taurus",
            5 => day <= 20 ? "Taurus" : "Gemini",
            6 => day <= 20 ? "Gemini" : "Cancer",
            7 => day <= 22 ? "Cancer" : "Leo",
            8 => day <= 22 ? "Leo" : "Virgo",
            9 => day <= 22 ? "Virgo" : "Libra",
            10 => day <= 22 ? "Libra" : "Scorpio",
            11 => day <= 21 ? "Scorpio" : "Sagittarius",
            12 => day <= 21 ? "Sagittarius" : "Capricorn",
            _ => "Unknown"
        };
    }
}
