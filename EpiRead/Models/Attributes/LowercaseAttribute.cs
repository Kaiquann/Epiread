using System.ComponentModel.DataAnnotations;

namespace EpiRead.Models.Attributes;

/**
 * @author: Chong Jun Xiang
 * @description: Custom validation attribute to ensure a string is lowercase.
 */
public class LowercaseAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is string str)
            return str == str.ToLower();
        return true;
    }
}