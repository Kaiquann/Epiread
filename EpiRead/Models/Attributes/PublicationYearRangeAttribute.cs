using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace EpiRead.Models.Attributes;

/// <summary>
///     Validate a year is within a valid range (min .. current year). Min must be provided
///     as a compile-time constant so it is usable in attribute declarations.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PublicationYearRangeAttribute(int min = Common.MinPublicationYear)
    : ValidationAttribute, IClientModelValidator
{
    public void AddValidation(ClientModelValidationContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-publicationyearrange",
            FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        MergeAttribute(context.Attributes, "data-val-publicationyearrange-min", min.ToString());
        MergeAttribute(context.Attributes, "data-val-publicationyearrange-max",
            Common.MaxPublicationYear.ToString());
    }

    public override bool IsValid(object? value)
    {
        // Allow nulls, other validators should handle 'Required' when necessary
        if (value == null) return true;

        if (value is int intValue) return intValue >= min && intValue <= Common.MaxPublicationYear;

        // Attempt to parse string values (e.g. form inputs)
        if (value is string s && int.TryParse(s, out int parsed))
            return parsed >= min && parsed <= Common.MaxPublicationYear;

        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return string.IsNullOrWhiteSpace(ErrorMessage)
            ? $"{name} must be between {min} and {Common.MaxPublicationYear}."
            : ErrorMessage;
    }

    private static void MergeAttribute(IDictionary<string, string> attributes, string key, string value)
    {
        if (attributes.ContainsKey(key)) return;
        attributes.Add(key, value);
    }
}