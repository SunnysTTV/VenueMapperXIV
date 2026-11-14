using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace VenueMapper.Services;

public static class SafeFile
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static void WriteAllText(string path, string contents) => _ = WriteAllTextAsync(path, contents);

    public static async Task<bool> WriteAllTextAsync(string path, string contents)
    {
        var storage = VenueMapperPlugin.FileStorage;
        var reliable = storage != null && Encoding.UTF8.GetByteCount(contents) <= storage.MaxFileSizeBytes;
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (reliable)
            {
                try
                {
                    await storage!.WriteAllTextAsync(path, contents).ConfigureAwait(false);
                    return true;
                }
                catch (Exception ex)
                {
                    VenueMapperPlugin.Log?.Debug($"[VenueMapper] Reliable write failed for {Path.GetFileName(path)}, using plain write: {ex.Message}");
                }
            }

            try
            {
                await File.WriteAllTextAsync(path, contents).ConfigureAwait(false);
                return true;
            }
            catch (Exception inner)
            {
                VenueMapperPlugin.Log?.Warning($"[VenueMapper] Could not write {Path.GetFileName(path)}: {inner.Message}");
                return false;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public static void Delete(string path) => _ = DeleteAsync(path);

    private static async Task DeleteAsync(string path)
    {
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            VenueMapperPlugin.Log?.Warning($"[VenueMapper] Could not delete {Path.GetFileName(path)}: {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }
}
