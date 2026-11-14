using System;
using System.Security.Cryptography;
using System.Text;

namespace VenueMapper.Services;

public static class OwnerIdHelper
{
    private const string Salt = "VenueMapper-OwnerID-2026";

    private sealed record CachedHash(ulong ContentId, string Hash);
    private static CachedHash? last;

    public static string ComputeHash(ulong contentId)
    {
        var cached = last;
        if (cached != null && cached.ContentId == contentId)
            return cached.Hash;

        var bytes = Encoding.UTF8.GetBytes(contentId.ToString() + Salt);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        last = new CachedHash(contentId, hash);
        return hash;
    }
}
