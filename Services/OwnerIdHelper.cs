using System;
using System.Security.Cryptography;
using System.Text;

namespace VenueMapper.Services;

public static class OwnerIdHelper
{
    private const string Salt = "VenueMapper-OwnerID-2026";

    public static string ComputeHash(ulong contentId)
    {
        var bytes = Encoding.UTF8.GetBytes(contentId.ToString() + Salt);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
