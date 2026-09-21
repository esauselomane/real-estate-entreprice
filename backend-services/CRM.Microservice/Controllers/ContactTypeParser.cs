using CRM.Microservice.Models;

namespace CRM.Microservice.Controllers;

public static class ContactTypeParser
{
    public static ContactType Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ContactType.None;

        var result = ContactType.None;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<ContactType>(part.Trim(), true, out var parsed))
                result |= parsed;
        }

        return result;
    }
}
