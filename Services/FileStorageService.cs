namespace SkillBridge.Services;

public record StoredFile(string RelativePath, string OriginalName);

/// <summary>
/// Stores uploads OUTSIDE wwwroot (App_Data/uploads) with random names, so files are only served
/// through controller actions that perform authorization checks.
/// Validates extension, size and file signature ("magic bytes").
/// </summary>
public class FileStorageService
{
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".zip"] = "application/zip",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain"
    };

    private readonly string _root;

    public FileStorageService(string rootPath)
    {
        _root = Path.GetFullPath(rootPath);
    }

    public static string AllowedExtensionsText => string.Join(", ", ContentTypes.Keys);

    public async Task<ServiceResult<StoredFile>> SaveAsync(IFormFile file, string folder)
    {
        if (file.Length == 0) return ServiceResult<StoredFile>.Fail("The uploaded file is empty.");
        if (file.Length > MaxBytes) return ServiceResult<StoredFile>.Fail("The file is larger than 10 MB.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !ContentTypes.ContainsKey(ext))
            return ServiceResult<StoredFile>.Fail($"File type not allowed. Allowed: {AllowedExtensionsText}.");

        var header = new byte[512];
        int read;
        await using (var s = file.OpenReadStream())
        {
            read = await s.ReadAsync(header.AsMemory(0, header.Length));
        }
        if (!SignatureMatches(ext, header.AsSpan(0, read)))
            return ServiceResult<StoredFile>.Fail("The file content does not match its extension.");

        var safeFolder = folder switch { "jobs" or "applications" or "submissions" => folder, _ => "misc" };
        var dir = Path.Combine(_root, safeFolder);
        Directory.CreateDirectory(dir);
        var storedName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var full = Path.Combine(dir, storedName);
        await using (var target = File.Create(full))
        {
            await file.CopyToAsync(target);
        }

        var original = Path.GetFileName(file.FileName);
        if (original.Length > 200) original = original[^200..];
        return ServiceResult<StoredFile>.Ok(new StoredFile($"{safeFolder}/{storedName}", original));
    }

    public static bool SignatureMatches(string ext, ReadOnlySpan<byte> h)
    {
        switch (ext.ToLowerInvariant())
        {
            case ".pdf": return h.Length >= 4 && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46; // %PDF
            case ".png": return h.Length >= 4 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
            case ".jpg":
            case ".jpeg": return h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
            case ".zip":
            case ".docx":
            case ".xlsx":
            case ".pptx": return h.Length >= 4 && h[0] == 0x50 && h[1] == 0x4B && h[2] == 0x03 && h[3] == 0x04; // PK..
            case ".txt": return !h.Contains((byte)0);
            default: return false;
        }
    }

    /// <summary>Opens a stored file. Returns null if the path is invalid or missing.</summary>
    public (Stream Stream, string ContentType)? Open(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
        var type = ContentTypes.TryGetValue(Path.GetExtension(full), out var t) ? t : "application/octet-stream";
        return (File.OpenRead(full), type);
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) File.Delete(full);
    }
}
