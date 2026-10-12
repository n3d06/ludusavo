using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ludusavo.Services;

public class GitHubService : IGitHubService
{
    private readonly IConfigService _configService;
    private readonly HttpClient _httpClient;

    public bool IsConfigured => _configService.Settings.IsGitHubConfigured;

    public GitHubService(IConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ludusavo-Desktop");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    private void ApplyAuth()
    {
        var token = _configService.Settings.GitHubToken;
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<(bool success, string? message)> TestConnectionAsync()
    {
        if (!IsConfigured)
        {
            return (false, "GitHub token, owner, or repo is not configured.");
        }

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}");
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var isPrivate = doc.RootElement.TryGetProperty("private", out var priv) && priv.GetBoolean();
                var fullName = doc.RootElement.GetProperty("full_name").GetString();
                return (true, $"Connected successfully to {fullName} ({(isPrivate ? "Private" : "Public")})");
            }
            else if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return (false, "Authentication failed: Personal Access Token is invalid or expired (401).");
            }
            else if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, $"Repository '{owner}/{repo}' not found (404).");
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                return (false, $"GitHub API error: {res.StatusCode} - {err}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    public async Task<List<string>> ListRemoteGameIdsAsync()
    {
        var results = new List<string>();
        if (!IsConfigured) return results;

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/saves");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "dir")
                        {
                            var name = item.GetProperty("name").GetString();
                            if (!string.IsNullOrEmpty(name)) results.Add(name);
                        }
                    }
                }
            }
        }
        catch { }

        return results;
    }

    public async Task<RemoteGameMeta?> GetRemoteMetaAsync(string gameId)
    {
        if (!IsConfigured) return null;

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/saves/{gameId}/meta.json");
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var contentProp))
            {
                var base64 = contentProp.GetString()?.Replace("\n", "").Replace("\r", "");
                if (!string.IsNullOrEmpty(base64))
                {
                    var bytes = Convert.FromBase64String(base64);
                    var rawMeta = Encoding.UTF8.GetString(bytes);
                    using var metaDoc = JsonDocument.Parse(rawMeta);

                    var root = metaDoc.RootElement;

                    string archiveHash = "";
                    if (root.TryGetProperty("archiveHash", out var ah)) archiveHash = ah.GetString() ?? "";
                    else if (root.TryGetProperty("sha256", out var s256)) archiveHash = s256.GetString() ?? "";

                    long totalSize = 0;
                    if (root.TryGetProperty("totalSize", out var ts) && ts.TryGetInt64(out var tsVal)) totalSize = tsVal;
                    else if (root.TryGetProperty("zipSize", out var zs) && zs.TryGetInt64(out var zsVal)) totalSize = zsVal;
                    else if (root.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var szVal)) totalSize = szVal;

                    var meta = new RemoteGameMeta
                    {
                        GameId = gameId,
                        GameName = root.TryGetProperty("gameName", out var gn) ? gn.GetString() ?? gameId : gameId,
                        ArchiveHash = archiveHash,
                        FileCount = root.TryGetProperty("fileCount", out var fc) && fc.TryGetInt32(out var fcVal) ? fcVal : 0,
                        TotalSize = totalSize,
                        SteamId = root.TryGetProperty("steamId", out var sid) && sid.ValueKind == JsonValueKind.Number && sid.TryGetInt32(out var sidVal) ? sidVal : null,
                        CustomBannerUrl = root.TryGetProperty("customBannerUrl", out var cb) && cb.ValueKind == JsonValueKind.String ? cb.GetString() : null
                    };

                    DateTime dt = DateTime.MinValue;
                    if (root.TryGetProperty("updatedAt", out var upProp) && DateTime.TryParse(upProp.GetString(), out var udt))
                    {
                        dt = udt.ToLocalTime();
                    }
                    else if (root.TryGetProperty("timestamp", out var timeProp) && DateTime.TryParse(timeProp.GetString(), out var tdt))
                    {
                        dt = tdt.ToLocalTime();
                    }
                    else if (root.TryGetProperty("lastModified", out var lmProp) && DateTime.TryParse(lmProp.GetString(), out var ldt))
                    {
                        dt = ldt.ToLocalTime();
                    }

                    meta.Timestamp = dt;
                    return meta;
                }
            }
        }
        catch { }

        return null;
    }

    private Dictionary<string, RemoteGameMeta>? _cachedRemoteMetas;

    public void InvalidateCatalogCache()
    {
        _cachedRemoteMetas = null;
    }

    public async Task<Dictionary<string, RemoteGameMeta>> GetAllRemoteMetasAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedRemoteMetas != null)
        {
            return _cachedRemoteMetas;
        }

        var result = new Dictionary<string, RemoteGameMeta>(StringComparer.OrdinalIgnoreCase);
        if (!IsConfigured) return result;

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/saves/catalog.json");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("content", out var contentProp))
                {
                    var base64 = contentProp.GetString()?.Replace("\n", "").Replace("\r", "");
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var bytes = Convert.FromBase64String(base64);
                        var rawCatalog = Encoding.UTF8.GetString(bytes);
                        var catalog = JsonSerializer.Deserialize<Dictionary<string, RemoteGameMeta>>(rawCatalog, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (catalog != null)
                        {
                            var resultDict = new Dictionary<string, RemoteGameMeta>(StringComparer.OrdinalIgnoreCase);
                            foreach (var kvp in catalog)
                            {
                                kvp.Value.GameId = kvp.Key;
                                resultDict[kvp.Key] = kvp.Value;
                            }
                            _cachedRemoteMetas = resultDict;
                            return resultDict;
                        }
                    }
                }
            }

            // Fallback (Migration): Fetch individually if catalog doesn't exist
            var gameIds = await ListRemoteGameIdsAsync();
            if (gameIds.Count > 0)
            {
                var tasks = gameIds.Select(async id => 
                {
                    var meta = await GetRemoteMetaAsync(id);
                    return (id, meta);
                });
                var results = await Task.WhenAll(tasks);
                foreach (var r in results)
                {
                    if (r.meta != null)
                    {
                        result[r.id] = r.meta;
                    }
                }
            }
        }
        catch { }

        _cachedRemoteMetas = result;
        return result;
    }

    private async Task<string?> GetFileShaAsync(string path)
    {
        try
        {
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;
            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/{path}");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sha", out var shaProp))
                {
                    return shaProp.GetString();
                }
            }
        }
        catch { }

        return null;
    }

    private async Task<(bool success, string? error)> UploadOrUpdateFileAsync(string repoPath, byte[] content, string commitMessage)
    {
        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var existingSha = await GetFileShaAsync(repoPath);
            var base64 = Convert.ToBase64String(content);

            var payload = new Dictionary<string, object>
            {
                { "message", commitMessage },
                { "content", base64 }
            };

            if (!string.IsNullOrEmpty(existingSha))
            {
                payload["sha"] = existingSha;
            }

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"repos/{owner}/{repo}/contents/{repoPath}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var res = await _httpClient.SendAsync(request);
            if (res.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var err = await res.Content.ReadAsStringAsync();
            return (false, $"Lỗi upload {repoPath}: {res.StatusCode} - {err}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(string branch, string headCommitSha)?> GetDefaultBranchAndHeadShaAsync()
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var repoRes = await _httpClient.GetAsync($"repos/{owner}/{repo}");
        if (!repoRes.IsSuccessStatusCode) return null;

        var repoJson = await repoRes.Content.ReadAsStringAsync();
        using var repoDoc = JsonDocument.Parse(repoJson);
        var defaultBranch = repoDoc.RootElement.TryGetProperty("default_branch", out var db)
            ? db.GetString() ?? "main"
            : "main";

        var refRes = await _httpClient.GetAsync($"repos/{owner}/{repo}/git/ref/heads/{defaultBranch}");
        if (!refRes.IsSuccessStatusCode) return null;

        var refJson = await refRes.Content.ReadAsStringAsync();
        using var refDoc = JsonDocument.Parse(refJson);
        var headCommitSha = refDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();

        if (string.IsNullOrEmpty(headCommitSha)) return null;
        return (defaultBranch, headCommitSha);
    }

    private async Task<string?> GetCommitTreeShaAsync(string commitSha)
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/git/commits/{commitSha}");
        if (!res.IsSuccessStatusCode) return null;

        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("tree").GetProperty("sha").GetString();
    }

    private async Task<string?> CreateBlobAsync(byte[] content)
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var payload = new
        {
            content = Convert.ToBase64String(content),
            encoding = "base64"
        };

        var json = JsonSerializer.Serialize(payload);
        using var contentObj = new StringContent(json, Encoding.UTF8, "application/json");
        var res = await _httpClient.PostAsync($"repos/{owner}/{repo}/git/blobs", contentObj);
        if (!res.IsSuccessStatusCode) return null;

        var resJson = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<string?> CreateTreeAsync(string baseTreeSha, List<(string path, string blobSha)> items)
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var treeArray = items.Select(i => new
        {
            path = i.path,
            mode = "100644",
            type = "blob",
            sha = i.blobSha
        }).ToArray();

        var payload = new
        {
            base_tree = baseTreeSha,
            tree = treeArray
        };

        var json = JsonSerializer.Serialize(payload);
        using var contentObj = new StringContent(json, Encoding.UTF8, "application/json");
        var res = await _httpClient.PostAsync($"repos/{owner}/{repo}/git/trees", contentObj);
        if (!res.IsSuccessStatusCode) return null;

        var resJson = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<string?> CreateCommitAsync(string message, string treeSha, string parentCommitSha)
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var payload = new
        {
            message = message,
            tree = treeSha,
            parents = new[] { parentCommitSha }
        };

        var json = JsonSerializer.Serialize(payload);
        using var contentObj = new StringContent(json, Encoding.UTF8, "application/json");
        var res = await _httpClient.PostAsync($"repos/{owner}/{repo}/git/commits", contentObj);
        if (!res.IsSuccessStatusCode) return null;

        var resJson = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<bool> UpdateBranchRefAsync(string branch, string commitSha)
    {
        ApplyAuth();
        var owner = _configService.Settings.GitHubOwner;
        var repo = _configService.Settings.GitHubRepo;

        var payload = new
        {
            sha = commitSha,
            force = false
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"repos/{owner}/{repo}/git/refs/heads/{branch}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var res = await _httpClient.SendAsync(request);
        return res.IsSuccessStatusCode;
    }

    public async Task<(bool success, string? error)> UploadGameSavesBatchAsync(List<GameBackupPayload> payloads, string commitMessage)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");

        if (payloads == null || payloads.Count == 0)
            return (true, null);

        try
        {
            var headInfo = await GetDefaultBranchAndHeadShaAsync();
            if (headInfo == null)
            {
                foreach (var p in payloads)
                {
                    var r = await UploadGameSaveLegacyAsync(p.GameId, p.ZipPath, p.MetaPath, p.MappingPath ?? string.Empty);
                    if (!r.success) return r;
                }
                return (true, null);
            }

            var (branch, headCommitSha) = headInfo.Value;

            var baseTreeSha = await GetCommitTreeShaAsync(headCommitSha);
            if (string.IsNullOrEmpty(baseTreeSha))
            {
                return (false, "Không thể lấy thông tin Tree của commit hiện tại.");
            }

            var treeItems = new List<(string path, string blobSha)>();
            var allMetas = await GetAllRemoteMetasAsync();

            foreach (var p in payloads)
            {
                if (!string.IsNullOrEmpty(p.MappingPath) && File.Exists(p.MappingPath))
                {
                    var mapBytes = await File.ReadAllBytesAsync(p.MappingPath);
                    var mapBlob = await CreateBlobAsync(mapBytes);
                    if (string.IsNullOrEmpty(mapBlob)) return (false, $"Lỗi tạo blob cho mapping.yaml của {p.GameName}");
                    treeItems.Add(($"saves/{p.GameId}/mapping.yaml", mapBlob));
                }

                if (File.Exists(p.ZipPath))
                {
                    var zipBytes = await File.ReadAllBytesAsync(p.ZipPath);
                    var zipBlob = await CreateBlobAsync(zipBytes);
                    if (string.IsNullOrEmpty(zipBlob)) return (false, $"Lỗi tạo blob cho latest.zip của {p.GameName}");
                    treeItems.Add(($"saves/{p.GameId}/latest.zip", zipBlob));
                }

                if (!string.IsNullOrEmpty(p.MetaPath) && File.Exists(p.MetaPath))
                {
                    var metaBytes = await File.ReadAllBytesAsync(p.MetaPath);
                    var metaBlob = await CreateBlobAsync(metaBytes);
                    if (string.IsNullOrEmpty(metaBlob)) return (false, $"Lỗi tạo blob cho meta.json của {p.GameName}");
                    treeItems.Add(($"saves/{p.GameId}/meta.json", metaBlob));

                    var metaText = Encoding.UTF8.GetString(metaBytes);
                    var metaObj = JsonSerializer.Deserialize<RemoteGameMeta>(metaText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (metaObj != null)
                    {
                        allMetas[p.GameId] = metaObj;
                    }
                }
            }

            var catalogJson = JsonSerializer.Serialize(allMetas, new JsonSerializerOptions { WriteIndented = true });
            var catalogBytes = Encoding.UTF8.GetBytes(catalogJson);
            var catalogBlob = await CreateBlobAsync(catalogBytes);
            if (string.IsNullOrEmpty(catalogBlob)) return (false, "Lỗi tạo blob cho catalog.json");
            treeItems.Add(("saves/catalog.json", catalogBlob));

            var newTreeSha = await CreateTreeAsync(baseTreeSha, treeItems);
            if (string.IsNullOrEmpty(newTreeSha)) return (false, "Lỗi tạo Git Tree.");

            var newCommitSha = await CreateCommitAsync(commitMessage, newTreeSha, headCommitSha);
            if (string.IsNullOrEmpty(newCommitSha)) return (false, "Lỗi tạo Git Commit.");

            var updated = await UpdateBranchRefAsync(branch, newCommitSha);
            if (!updated) return (false, "Lỗi cập nhật Branch Reference (HEAD).");

            _cachedRemoteMetas = allMetas;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> UploadGameSaveAsync(string gameId, string zipPath, string metaPath, string mappingPath)
    {
        var payload = new GameBackupPayload
        {
            GameId = gameId,
            GameName = gameId,
            ZipPath = zipPath,
            MetaPath = metaPath,
            MappingPath = mappingPath
        };
        return await UploadGameSavesBatchAsync(new List<GameBackupPayload> { payload }, $"Backup save: {gameId}");
    }

    private async Task<(bool success, string? error)> UploadGameSaveLegacyAsync(string gameId, string zipPath, string metaPath, string mappingPath)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");

        if (!File.Exists(zipPath))
            return (false, $"File {zipPath} không tồn tại.");

        try
        {
            if (File.Exists(mappingPath))
            {
                var mappingBytes = await File.ReadAllBytesAsync(mappingPath);
                var rMap = await UploadOrUpdateFileAsync($"saves/{gameId}/mapping.yaml", mappingBytes, $"Sync mapping for {gameId}");
                if (!rMap.success) return rMap;
            }

            var zipBytes = await File.ReadAllBytesAsync(zipPath);
            var rZip = await UploadOrUpdateFileAsync($"saves/{gameId}/latest.zip", zipBytes, $"Sync save archive for {gameId}");
            if (!rZip.success) return rZip;

            if (File.Exists(metaPath))
            {
                var metaBytes = await File.ReadAllBytesAsync(metaPath);
                var rMeta = await UploadOrUpdateFileAsync($"saves/{gameId}/meta.json", metaBytes, $"Sync meta for {gameId}");
                if (!rMeta.success) return rMeta;
            }

            try
            {
                var allMetas = await GetAllRemoteMetasAsync();
                if (File.Exists(metaPath))
                {
                    var metaContent = await File.ReadAllTextAsync(metaPath);
                    var newMeta = JsonSerializer.Deserialize<RemoteGameMeta>(metaContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (newMeta != null)
                    {
                        allMetas[gameId] = newMeta;
                        _cachedRemoteMetas = allMetas;
                        var catalogJson = JsonSerializer.Serialize(allMetas, new JsonSerializerOptions { WriteIndented = true });
                        var catalogBytes = Encoding.UTF8.GetBytes(catalogJson);
                        await UploadOrUpdateFileAsync("saves/catalog.json", catalogBytes, $"Update catalog after syncing {gameId}");
                    }
                }
            }
            catch { }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DownloadGameSaveAsync(string gameId, string targetDir)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");

        try
        {
            ApplyAuth();
            Directory.CreateDirectory(targetDir);

            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            // Download files: meta.json, mapping.yaml, latest.zip
            var filesToDownload = new[] { "latest.zip", "meta.json", "mapping.yaml" };
            foreach (var filename in filesToDownload)
            {
                var remotePath = $"saves/{gameId}/{filename}";
                var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/{remotePath}");
                if (!res.IsSuccessStatusCode)
                {
                    if (filename == "mapping.yaml") continue; // optional
                    return (false, $"Không tải được {remotePath}: {res.StatusCode}");
                }

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var localDest = Path.Combine(targetDir, filename);

                if (root.TryGetProperty("download_url", out var dlUrlProp) && !string.IsNullOrEmpty(dlUrlProp.GetString()))
                {
                    var dlUrl = dlUrlProp.GetString()!;
                    using var dlRes = await _httpClient.GetAsync(dlUrl);
                    if (dlRes.IsSuccessStatusCode)
                    {
                        await using var fs = File.Create(localDest);
                        await dlRes.Content.CopyToAsync(fs);
                        continue;
                    }
                }

                if (root.TryGetProperty("content", out var cProp))
                {
                    var b64 = cProp.GetString()?.Replace("\n", "").Replace("\r", "");
                    if (!string.IsNullOrEmpty(b64))
                    {
                        var bytes = Convert.FromBase64String(b64);
                        await File.WriteAllBytesAsync(localDest, bytes);
                    }
                }
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> UploadFileAsync(string repoPath, string localPath, string commitMessage)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");
            
        if (!File.Exists(localPath))
            return (false, $"File {localPath} không tồn tại.");
            
        try
        {
            var fileBytes = await File.ReadAllBytesAsync(localPath);
            return await UploadOrUpdateFileAsync(repoPath, fileBytes, commitMessage);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DownloadFileAsync(string repoPath, string localPath)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/{repoPath}");
            if (!res.IsSuccessStatusCode)
            {
                return (false, $"Không tải được {repoPath}: {res.StatusCode}");
            }

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (root.TryGetProperty("download_url", out var dlUrlProp) && !string.IsNullOrEmpty(dlUrlProp.GetString()))
            {
                var dlUrl = dlUrlProp.GetString()!;
                using var dlRes = await _httpClient.GetAsync(dlUrl);
                if (dlRes.IsSuccessStatusCode)
                {
                    await using var fs = File.Create(localPath);
                    await dlRes.Content.CopyToAsync(fs);
                    return (true, null);
                }
            }

            if (root.TryGetProperty("content", out var cProp))
            {
                var b64 = cProp.GetString()?.Replace("\n", "").Replace("\r", "");
                if (!string.IsNullOrEmpty(b64))
                {
                    var bytes = Convert.FromBase64String(b64);
                    await File.WriteAllBytesAsync(localPath, bytes);
                    return (true, null);
                }
            }
            
            return (false, "Không lấy được nội dung file");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool success, string? error)> DeleteSingleFileAsync(string repoPath, string? sha, string commitMessage)
    {
        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            if (string.IsNullOrEmpty(sha))
            {
                sha = await GetFileShaAsync(repoPath);
            }

            if (string.IsNullOrEmpty(sha))
            {
                return (true, null); // File does not exist
            }

            var payload = new Dictionary<string, object>
            {
                { "message", commitMessage },
                { "sha", sha }
            };

            var json = JsonSerializer.Serialize(payload);
            var encodedPath = string.Join("/", repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"repos/{owner}/{repo}/contents/{encodedPath}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var res = await _httpClient.SendAsync(request);
            if (res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (true, null);
            }

            var err = await res.Content.ReadAsStringAsync();
            return (false, $"Lỗi xóa {repoPath}: {res.StatusCode} - {err}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DeleteGameSaveAsync(string gameId)
    {
        if (!IsConfigured)
            return (false, "GitHub chưa được cấu hình");

        try
        {
            ApplyAuth();
            var owner = _configService.Settings.GitHubOwner;
            var repo = _configService.Settings.GitHubRepo;

            var encodedGameId = Uri.EscapeDataString(gameId);
            var res = await _httpClient.GetAsync($"repos/{owner}/{repo}/contents/saves/{encodedGameId}");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var filePath = item.TryGetProperty("path", out var p) ? p.GetString() : null;
                        var sha = item.TryGetProperty("sha", out var s) ? s.GetString() : null;
                        var fileName = item.TryGetProperty("name", out var n) ? n.GetString() : "file";

                        if (!string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(sha))
                        {
                            var delResult = await DeleteSingleFileAsync(filePath, sha, $"Delete {fileName} for {gameId}");
                            if (!delResult.success)
                            {
                                return delResult;
                            }
                        }
                    }
                }
            }
            else
            {
                // Fallback: Try deleting known save files directly
                var standardFiles = new[] { "latest.zip", "meta.json", "mapping.yaml" };
                foreach (var fileName in standardFiles)
                {
                    var filePath = $"saves/{gameId}/{fileName}";
                    var sha = await GetFileShaAsync(filePath);
                    if (!string.IsNullOrEmpty(sha))
                    {
                        var delResult = await DeleteSingleFileAsync(filePath, sha, $"Delete {fileName} for {gameId}");
                        if (!delResult.success)
                        {
                            return delResult;
                        }
                    }
                }
            }

            // Remove gameId from saves/catalog.json
            try
            {
                var allMetas = await GetAllRemoteMetasAsync();
                if (allMetas.ContainsKey(gameId))
                {
                    allMetas.Remove(gameId);
                    _cachedRemoteMetas = allMetas;
                    var catalogJson = JsonSerializer.Serialize(allMetas, new JsonSerializerOptions { WriteIndented = true });
                    var catalogBytes = Encoding.UTF8.GetBytes(catalogJson);
                    await UploadOrUpdateFileAsync("saves/catalog.json", catalogBytes, $"Remove {gameId} from catalog");
                }
            }
            catch { }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(int remaining, int limit, int resetMinutes)> GetRateLimitAsync()
    {
        try
        {
            ApplyAuth();
            var res = await _httpClient.GetAsync("rate_limit");
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("rate", out var rateProp))
                {
                    var rem = rateProp.GetProperty("remaining").GetInt32();
                    var lim = rateProp.GetProperty("limit").GetInt32();
                    var resetEpoch = rateProp.GetProperty("reset").GetInt64();
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);
                    var minutes = Math.Max(0, (int)(resetTime - DateTimeOffset.UtcNow).TotalMinutes);
                    return (rem, lim, minutes);
                }
            }
        }
        catch { }

        return (0, 0, 0);
    }
}
