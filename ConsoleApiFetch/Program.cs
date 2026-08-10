using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Linq.Expressions;
//using static System.Runtime.InteropServices.JavaScript.JSType;

class Program
{

    private static string Organization {  get; set; }
    private static string Project{ get; set; }
    private static string Pat {  get; set; }
    private static string Repo { get; set; }

    private static Dictionary<string, List<string>> fileContents = new Dictionary<string, List<string>>();


    // Keep args for inserting branch-ID;s on program call
    static async Task Main(string[] args)
    {

        try
        {
            // Open current application configuration
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            KeyValueConfigurationCollection section = config.AppSettings.Settings;

            Organization = section["Organization"].Value;
            Project = section["project"].Value;
            Pat = section["pat"].Value;
            Repo = section["repo"].Value;
        }
        catch (ConfigurationErrorsException ex)
        {
            Console.WriteLine("Error reading configuration: ");
            Console.WriteLine(ex.Message);
        }

        // Rename to branchId
        //string branchId = "feature/1243195";
        string branchId = "SSIS_Update";
        if (branchId == null || branchId == "")
        {
            Console.Write("Enter branchID: ");
            branchId = Console.ReadLine();
        }

        try
        {
            var repos = await GetRepos(Project);
            string fullRefName = await GetFeatureName(branchId);
            Console.WriteLine($"Feature Reference: {fullRefName}");

            // Extract the feature name and folder path
            string featureName = fullRefName.Split('/').Last();
            string baseFolder = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings["branchIdBaseFolder"].Value;
            string featureFolderPath = Path.Combine(baseFolder, featureName);

            List<PushInfo> pushInfos = await GetPushIdsWithDates(fullRefName);

            List<string> pushIds = await GetPushIds(fullRefName);
            foreach (var pushId in pushIds)
            {
                Console.WriteLine($"Processing PushID: {pushId}");
                string commitId = await GetCommitId(pushId);

                if (commitId == "Unknown Commit" || commitId == "No Commit")
                {
                    Console.WriteLine(commitId);
                    continue;
                }
                else
                {
                    Console.WriteLine($"CommitID: {commitId}");

                    List<string> parents = await GetCommitParents(commitId);
                    Console.WriteLine($"Parents: {string.Join(", ", parents)}");

                    var pushInfo = pushInfos.Where(p =>String.Equals(p.PushId, pushId)).ToList().First();

                    foreach (var parentId in parents)
                    {
                        var changes = await GetCommitChangesFromParent(commitId);
                        ProcessChanges(changes, pushInfo, featureFolderPath, parents); // Pass the feature folder path
                    }
                }
            }

            
            foreach (var pushInfo in pushInfos.OrderBy(p => p.Date))
            {
                Console.WriteLine($"PushID: {pushInfo.PushId}, Date: {pushInfo.Date}");
            }

            // After all commits and changes have been processed:
            foreach (var kvp in fileContents)
            {
                var finalPath = Path.Combine(featureFolderPath, Path.GetFileName(kvp.Key));
                File.WriteAllLines(finalPath, kvp.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task<string> GetRepos(string project)
    {
        string url = $"https://dev.azure.com/{Organization}/{project}/_apis/git/repositories";
        string repoId;
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var contentJson = JObject.Parse(content);
            repoId = contentJson["value"][0]["id"].ToString();
        }
        return repoId;
    }


    private static async Task<string> GetFeatureName(string branchId)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/refs?filter=heads/{branchId}&api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            // Extract the full reference name
            string fullRefName = json["value"]?[0]?["name"]?.ToString();
            if (string.IsNullOrEmpty(fullRefName))
            {
                throw new Exception("Feature reference not found.");
            }

            // Extract the feature name (e.g., "1243195_MatchningsrutinFörNyhetsbrev")
            string featureName = fullRefName.Split('/').Last();

            // Create a folder for the feature
             string baseFolder = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings["branchIdBaseFolder"].Value;
            string featureFolderPath = Path.Combine(baseFolder, featureName);

            if (!Directory.Exists(featureFolderPath))
            {
                Directory.CreateDirectory(featureFolderPath);
                Console.WriteLine($"Created folder: {featureFolderPath}");
            }

            return fullRefName; // Return the full reference name
        }
    }

    private class PushInfo
    {
        public string PushId { get; set; }
        public DateTime Date { get; set; }
    }

    private static async Task<List<PushInfo>> GetPushIdsWithDates(string fullRefName)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/pushes?searchCriteria.refName={fullRefName}&api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var pushInfos = new List<PushInfo>();

            var pushes = json["value"];
            if (pushes != null && pushes.Count() > 1)
            {
                for (int i = 0; i < pushes.Count() - 1; i++)
                {
                    pushInfos.Add(new PushInfo
                    {
                        PushId = pushes[i]["pushId"].ToString(),
                        Date = DateTime.Parse(pushes[i]["date"].ToString())
                    });
                }
            }

            return pushInfos;
        }
    }

    private static async Task<List<string>> GetPushIds(string fullRefName)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/pushes?searchCriteria.refName={fullRefName}&api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            var pushIds = new List<string>();

            var pushes = json["value"];
            if (pushes != null && pushes.Count() > 1) // Ensure there are at least two pushes
            {
                for (int i = 0; i < pushes.Count(); i++) // Tagit bort "skip last push". Se om det är olika när en extra push dyker upp i en "Commit kedja". För: 1312000 så finns det endast 2 stycken commits
                {
                    pushIds.Add(pushes[i]["pushId"].ToString());
                }
            }

            return pushIds;
        }
    }

    private static async Task<string> GetCommitId(string pushId)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/pushes/{pushId}?api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            if (json["commits"].Count() > 0)
            {
                return json["commits"]?[0]?["commitId"]?.ToString() ?? "Unknown Commit";
            }
            else
            {
                return "No Commit";
            }
        }
    }

    private static async Task<JArray> GetCommitChanges(string commitId)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/commits/{commitId}/changes?api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            return (JArray)json["changes"];
        }
    }

    private static async Task<JArray> GetCommitChangesFromParent(string commitId)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/commits/{commitId}/changes?api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            return (JArray)json["changes"];
        }
    }

    private static async Task<List<string>> GetCommitParents(string commitId)
    {
        string url = $"https://dev.azure.com/{Organization}/{Project}/_apis/git/repositories/{Repo}/commits/{commitId}?api-version=7.0";
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            // Extract the parents
            var parents = json["parents"]?.ToObject<List<string>>();
            if (parents == null || parents.Count == 0)
            {
                throw new Exception("No parents found for the commit.");
            }

            // Save for error-checking
            // Skip commits with more than one parent
            //if (parents.Count > 1)
            //{
            //    Console.WriteLine($"Skipping commit {commitId} as it has multiple parents.");
            //    return new List<string>(); // Return an empty list to indicate skipping
            //}

            return parents;
        }
    }
    private static void ProcessChanges(JArray changes, PushInfo pushInfo, string featureFolderPath, List<string> parents)
    //private static void ProcessChanges(JArray commitInfo, string featureFolderPath, List<string> parents)
    {
        // Skip processing if there are no parents (e.g., skipped in GetCommitParents)
        if (parents == null || parents.Count == 0)
        {
            Console.WriteLine("Skipping ProcessChanges as there are no valid parents.");
            return;
        }

        foreach (var change in changes)
        {
            string changeType = change["changeType"].ToString();
            string path = change["item"]?["path"]?.ToString();
            string url = change["item"]?["url"]?.ToString();
            string gitObjectType = change["item"]?["gitObjectType"]?.ToString();

            // Step 1: Check if gitObjectType is "blob" and path/url are valid
            if (gitObjectType == "blob" && !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(url))
            {
                string fileExtension = Path.GetExtension(path);
                string fileName = path.TrimStart('/').Replace('/', '.');
                string savePath = Path.Combine(featureFolderPath, fileName);
                string diffFolder = Path.Combine(featureFolderPath, "DiffFiles");
                string diffFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-diff{fileExtension}";
                string diffFilePath = Path.Combine(diffFolder, diffFileName);
                if (!Directory.Exists(diffFolder))
                {
                    Directory.CreateDirectory(diffFolder);
                }

                // Step 2: Check if the file extension is approved
                if (fileExtension.Equals(".sql", StringComparison.OrdinalIgnoreCase) ||
                    fileExtension.Equals(".dtproj", StringComparison.OrdinalIgnoreCase) ||
                    fileExtension.Equals(".dtsx", StringComparison.OrdinalIgnoreCase) ||
                    fileExtension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                    fileExtension.Equals(".sqlproj", StringComparison.OrdinalIgnoreCase))
                {
                    // Step 3: Check the changeType and handle accordingly
                    if (changeType.Contains("edit"))
                    {
                        // Handle "Edit": Download the file and the original file
                        try
                        {
                            Console.WriteLine($"Downloading edited file: {path}");
                            DownloadFile(url, savePath).Wait();
                            Console.WriteLine($"File saved to: {savePath}");

                            // Use the parent commit ID from the parents list
                            string parentCommitId = parents.FirstOrDefault();
                            if (!string.IsNullOrEmpty(parentCommitId))
                            {

                                // If both "edit" and "rename" are present, bypass original file download
                                if (changeType.Contains("edit") && changeType.Contains("rename"))
                                {
                                    // Compare the file to itself (diff will be empty or just show new content)
                                    GenerateDiffFile(savePath, savePath, diffFilePath, path, $"{pushInfo.Date.ToShortDateString()} : {pushInfo.Date.ToShortTimeString()}");
                                    Console.WriteLine($"Diff file created (edit+rename bypass): {diffFilePath}");
                                }
                                else
                                {
                                    // Normal logic: download original file and compare
                                    string originalFileUrl = $"{url}&versionDescriptor.version={parentCommitId}";
                                    string originalFileName = $"{Path.GetFileNameWithoutExtension(fileName)}-original{fileExtension}";
                                    string originalFilePath = Path.Combine(featureFolderPath, originalFileName);

                                    Console.WriteLine($"Downloading original file: {path}");
                                    DownloadFile(originalFileUrl, originalFilePath).Wait();
                                    Console.WriteLine($"Original file saved to: {originalFilePath}");

                                    GenerateDiffFile(savePath, originalFilePath, diffFilePath, path, $"{pushInfo.Date.ToShortDateString()} : {pushInfo.Date.ToShortTimeString()}");
                                    Console.WriteLine($"Diff file created: {diffFilePath}");
                                }

                                // After generating diff file, parse it:
                                var diffLines = File.ReadAllLines(diffFilePath);
                                var addedLines = new List<string>();
                                var removedLines = new List<string>();
                                bool isAdded = false, isRemoved = false;
                                foreach (var line in diffLines)
                                {
                                    if (line.StartsWith("=== Added Lines ===")) { isAdded = true; isRemoved = false; continue; }
                                    if (line.StartsWith("=== Removed Lines ===")) { isAdded = false; isRemoved = true; continue; }
                                    if (string.IsNullOrWhiteSpace(line)) continue;
                                    if (isAdded) addedLines.Add(line);
                                    if (isRemoved) removedLines.Add(line);
                                }
                                ApplyDiff(path, addedLines, removedLines);
                            }
                            else
                            {
                                Console.WriteLine($"Parent commit ID not found for: {path}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing edited file: {path}");
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else if (changeType.Contains("add"))
                    {
                        // Handle "Add": Download the file
                        try
                        {
                            Console.WriteLine($"Downloading added file: {path}");
                            DownloadFile(url, savePath).Wait();
                            Console.WriteLine($"File saved to: {savePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error downloading added file: {path}");
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else if (changeType.Contains("delete"))
                    {
                        // Handle "Delete": Create a file with "-deleted"
                        string deletedFileName = $"{fileName}-deleted.txt";
                        string deletedFilePath = Path.Combine(featureFolderPath, deletedFileName);
                        try
                        {
                            File.WriteAllText(deletedFilePath, $"File deleted: {path}");
                            Console.WriteLine($"Created deleted marker file: {deletedFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating deleted marker file for: {path}");
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else if (changeType.Contains("rename"))
                    {
                        // Download the file if necessary
                        if (!File.Exists(savePath))
                        {
                            Console.WriteLine($"Downloading renamed file: {path}");
                            DownloadFile(url, savePath).Wait();
                            Console.WriteLine($"File saved to: {savePath}");
                        }

                        var addedLines = File.ReadAllLines(savePath).ToList();
                        var diffContent = new List<string>
                        {
                            $"Path: {path}",
                            "=== Added Lines ==="
                        };
                        diffContent.AddRange(addedLines);
                        diffContent.Add("");
                        diffContent.Add("=== Removed Lines ===");
                        // No removed lines for a pure rename

                        File.WriteAllLines(diffFilePath, diffContent);
                        Console.WriteLine($"Diff file created for rename: {diffFilePath}");

                        // Optionally, still create the rename marker file
                        string renamedFileName = $"{fileName}-rename.txt";
                        string renamedFilePath = Path.Combine(featureFolderPath, renamedFileName);
                        try
                        {
                            File.WriteAllText(renamedFilePath, $"File renamed: {path}");
                            Console.WriteLine($"Created renamed marker file: {renamedFilePath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating renamed marker file for: {path}");
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping unsupported file: {path}");
                }
            }
        }
    }

    private static async Task DownloadFile(string fileUrl, string savePath)
    {
        using (var client = CreateHttpClient())
        {
            var response = await client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();

            using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
            }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{Pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }

    private static void GenerateDiffFile(string editedFilePath, string originalFilePath, string diffFilePath, string path, string date)
    {
        try
        {
            // No folder creation here, just use diffFilePath

            var editedLines = File.ReadAllLines(editedFilePath);
            var originalLines = File.ReadAllLines(originalFilePath);

            // Why remove white spaces?
            // Normalizing does not seem to do anything
            Func<string, string> normalize = line => string.Concat(line.Where(c => !char.IsWhiteSpace(c)));
            var normalizedOriginal = new HashSet<string>(originalLines.Select(normalize));
            var normalizedEdited = new HashSet<string>(editedLines.Select(normalize));

            var addedLines = editedLines.Where(line => !normalizedOriginal.Contains(normalize(line))).ToList();
            var removedLines = originalLines.Where(line => !normalizedEdited.Contains(normalize(line))).ToList();

            var diffContent = new List<string>();
            if (!File.Exists(diffFilePath))
            {
                File.Create(diffFilePath);
            }
            foreach (var line in File.ReadAllLines(diffFilePath))
            {
                diffContent.Add(normalize(line));
            }
            //diffContent.Add();
            diffContent.Add($"Path: {path}\t|\t{date}");
            diffContent.Add("=== Added Lines ===");
            diffContent.AddRange(addedLines);
            diffContent.Add("");
            diffContent.Add("=== Removed Lines ===");
            diffContent.AddRange(removedLines);

            File.WriteAllLines(diffFilePath, diffContent);
            Console.WriteLine($"Diff file created successfully: {diffFilePath}");

            ApplyDiff(path, addedLines, removedLines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating diff file: {diffFilePath}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void ApplyDiff(string path, List<string> addedLines, List<string> removedLines)
    {
        if (!fileContents.ContainsKey(path))
            fileContents[path] = new List<string>();

        // Remove lines
        foreach (var line in removedLines)
        {
            fileContents[path].RemoveAll(l => Normalize(l) == Normalize(line));
        }

        // Add lines
        foreach (var line in addedLines)
        {
            if (!fileContents[path].Any(l => Normalize(l) == Normalize(line)))
                fileContents[path].Add(line);
        }
    }

    private static string Normalize(string line)
    {
        return string.Concat(line.Where(c => !char.IsWhiteSpace(c)));
    }
    //private static void set_organization(string Organization)


}
