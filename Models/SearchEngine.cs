using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FuzzySharp; // Added missing reference

namespace ExplorerPro.Models
{
    /// <summary>
    /// Provides file search capabilities with various methods for finding files and folders.
    /// </summary>
    public class SearchEngine
    {
        private readonly ILogger<SearchEngine>? _logger;
        private readonly IFuzzyMatcher _fuzzyMatcher;
        
        /// <summary>
        /// Initializes a new instance of the SearchEngine class.
        /// </summary>
        /// <param name="fuzzyMatcher">The fuzzy matching implementation to use.</param>
        /// <param name="logger">Optional logger for tracking operations.</param>
        public SearchEngine(IFuzzyMatcher fuzzyMatcher, ILogger<SearchEngine>? logger = null)
        {
            _fuzzyMatcher = fuzzyMatcher ?? throw new ArgumentNullException(nameof(fuzzyMatcher));
            _logger = logger;
        }

        /// <summary>
        /// Searches for files and folders by exact substring match within the specified directory.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="includeFolders">Whether to include folders in the results.</param>
        /// <param name="depth">The maximum depth to search (null for unlimited).</param>
        /// <returns>A list of matching file and folder paths.</returns>
        public List<string> SearchByName(string directory, string query, bool includeFolders = true, int? depth = null)
        {
            var results = new List<string>();
            try
            {
                SearchDirectoryByName(directory, query, results, includeFolders, depth);
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Search failed in {directory}");
            }
            return results;
        }

        private void SearchDirectoryByName(string directory, string query, List<string> results, bool includeFolders, int? depth)
        {
            if (depth.HasValue && depth.Value <= 0)
                return;

            try
            {
                // Process directories
                if (includeFolders)
                {
                    foreach (var dir in Directory.GetDirectories(directory))
                    {
                        try
                        {
                            string dirName = Path.GetFileName(dir);
                            if (dirName.ToLower().Contains(query.ToLower()))
                            {
                                results.Add(dir);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error processing directory: {dir}");
                        }
                    }
                }

                // Process files
                foreach (var file in Directory.GetFiles(directory))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.ToLower().Contains(query.ToLower()))
                        {
                            results.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing file: {file}");
                    }
                }

                // Process subdirectories
                int? newDepth = depth.HasValue ? depth.Value - 1 : null;
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        SearchDirectoryByName(dir, query, results, includeFolders, newDepth);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger?.LogError($"Permission denied: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error searching subdirectory: {dir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error searching directory: {directory}");
            }
        }

        /// <summary>
        /// Searches for files and folders by fuzzy matching within the specified directory.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="threshold">Minimum similarity score (0-100) to consider a match.</param>
        /// <param name="includeFolders">Whether to include folders in the results.</param>
        /// <param name="depth">The maximum depth to search (null for unlimited).</param>
        /// <returns>A list of matching file and folder paths.</returns>
        public List<string> FuzzySearchByName(string directory, string query, int threshold = 60, bool includeFolders = true, int? depth = null)
        {
            var results = new List<string>();
            try
            {
                FuzzySearchDirectoryByName(directory, query, results, threshold, includeFolders, depth);
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Fuzzy search failed in {directory}");
            }
            return results;
        }

        private void FuzzySearchDirectoryByName(string directory, string query, List<string> results, int threshold, bool includeFolders, int? depth)
        {
            if (depth.HasValue && depth.Value <= 0)
                return;

            try
            {
                // Process directories
                if (includeFolders)
                {
                    foreach (var dir in Directory.GetDirectories(directory))
                    {
                        try
                        {
                            string dirName = Path.GetFileName(dir);
                            int score = _fuzzyMatcher.PartialRatio(query.ToLower(), dirName.ToLower());
                            if (score >= threshold)
                            {
                                results.Add(dir);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Error processing directory: {dir}");
                        }
                    }
                }

                // Process files
                foreach (var file in Directory.GetFiles(directory))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        int score = _fuzzyMatcher.PartialRatio(query.ToLower(), fileName.ToLower());
                        if (score >= threshold)
                        {
                            results.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing file: {file}");
                    }
                }

                // Process subdirectories
                int? newDepth = depth.HasValue ? depth.Value - 1 : null;
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        FuzzySearchDirectoryByName(dir, query, results, threshold, includeFolders, newDepth);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger?.LogError($"Permission denied: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error fuzzy searching subdirectory: {dir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error fuzzy searching directory: {directory}");
            }
        }

        /// <summary>
        /// Performs an advanced search with optional filters for file type, size, and date range.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="fileType">The file extension to filter by (null for any).</param>
        /// <param name="sizeRange">A tuple with minimum and maximum file size in bytes (null for any).</param>
        /// <param name="dateRange">A tuple with start and end dates (null for any).</param>
        /// <returns>A list of matching file paths.</returns>
        public List<string> SearchWithFilters(string directory, string query, string? fileType = null, Tuple<long, long>? sizeRange = null, Tuple<DateTime, DateTime>? dateRange = null)
        {
            var results = new List<string>();
            try
            {
                SearchDirectoryWithFilters(directory, query, results, fileType, sizeRange, dateRange);
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Filtered search failed in {directory}");
            }
            return results;
        }

        private void SearchDirectoryWithFilters(string directory, string query, List<string> results, string? fileType, Tuple<long, long>? sizeRange, Tuple<DateTime, DateTime>? dateRange)
        {
            try
            {
                // Process files
                foreach (var file in Directory.GetFiles(directory))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        
                        // Basic name check (exact substring)
                        if (!fileName.ToLower().Contains(query.ToLower()))
                            continue;

                        // Filter by file type
                        if (fileType != null && !fileName.ToLower().EndsWith(fileType.ToLower()))
                            continue;

                        // Filter by size
                        if (sizeRange != null)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                long fileSize = fileInfo.Length;
                                if (fileSize < sizeRange.Item1 || fileSize > sizeRange.Item2)
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, $"Could not retrieve size for {file}");
                                continue;
                            }
                        }

                        // Filter by date
                        if (dateRange != null)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                DateTime fileDate = fileInfo.LastWriteTime;
                                if (fileDate < dateRange.Item1 || fileDate > dateRange.Item2)
                                    continue;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, $"Could not retrieve date for {file}");
                                continue;
                            }
                        }

                        results.Add(file);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing file: {file}");
                    }
                }

                // Process subdirectories
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        SearchDirectoryWithFilters(dir, query, results, fileType, sizeRange, dateRange);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger?.LogError($"Permission denied: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error searching subdirectory with filters: {dir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error searching directory with filters: {directory}");
            }
        }

        /// <summary>
        /// Searches for content within PDF and DOCX files.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">The maximum number of results to return.</param>
        /// <returns>A list of matching file paths.</returns>
        public List<string> SearchFileContent(string directory, string query, int maxResults = 10)
        {
            var results = new List<string>();
            try
            {
                SearchDirectoryFileContent(directory, query, results, maxResults);
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Content search failed in {directory}");
            }
            return results;
        }

        private void SearchDirectoryFileContent(string directory, string query, List<string> results, int maxResults)
        {
            if (results.Count >= maxResults)
                return;

            try
            {
                // Process files
                foreach (var file in Directory.GetFiles(directory))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        string extension = Path.GetExtension(fileName).ToLower();

                        // Skip unsupported files
                        if (extension != ".pdf" && extension != ".docx")
                            continue;

                        try
                        {
                            bool found = false;

                            if (extension == ".pdf")
                            {
                                using (var document = PdfDocument.Open(file))
                                {
                                    foreach (var page in document.GetPages())
                                    {
                                        var text = page.Text;
                                        if (text != null && text.ToLower().Contains(query.ToLower()))
                                        {
                                            found = true;
                                            break;  // Stop after first match
                                        }
                                    }
                                }
                            }
                            else if (extension == ".docx")
                            {
                                using (var document = WordprocessingDocument.Open(file, false))
                                {
                                    var body = document.MainDocumentPart?.Document.Body;
                                    if (body != null)
                                    {
                                        foreach (var paragraph in body.Elements<Paragraph>())
                                        {
                                            string text = paragraph.InnerText;
                                            if (text != null && text.ToLower().Contains(query.ToLower()))
                                            {
                                                found = true;
                                                break;  // Stop after first match
                                            }
                                        }
                                    }
                                }
                            }

                            if (found)
                            {
                                results.Add(file);
                                if (results.Count >= maxResults)
                                    return;  // Stop early if max results are found
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"Failed to read {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing file: {file}");
                    }
                }

                // Process subdirectories
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    try
                    {
                        SearchDirectoryFileContent(dir, query, results, maxResults);
                        if (results.Count >= maxResults)
                            return;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger?.LogError($"Permission denied: {dir}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error searching subdirectory for content: {dir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                _logger?.LogError($"Permission denied: {directory}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error searching directory for content: {directory}");
            }
        }
        
        /// <summary>
        /// Asynchronously searches for files and folders by exact substring match within the specified directory.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="includeFolders">Whether to include folders in the results.</param>
        /// <param name="depth">The maximum depth to search (null for unlimited).</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of matching file and folder paths.</returns>
        public async Task<List<string>> SearchByNameAsync(string directory, string query, bool includeFolders = true, int? depth = null)
        {
            return await Task.Run(() => SearchByName(directory, query, includeFolders, depth));
        }
        
        /// <summary>
        /// Asynchronously searches for files and folders by fuzzy matching within the specified directory.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="threshold">Minimum similarity score (0-100) to consider a match.</param>
        /// <param name="includeFolders">Whether to include folders in the results.</param>
        /// <param name="depth">The maximum depth to search (null for unlimited).</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of matching file and folder paths.</returns>
        public async Task<List<string>> FuzzySearchByNameAsync(string directory, string query, int threshold = 60, bool includeFolders = true, int? depth = null)
        {
            return await Task.Run(() => FuzzySearchByName(directory, query, threshold, includeFolders, depth));
        }
        
        /// <summary>
        /// Asynchronously performs an advanced search with optional filters for file type, size, and date range.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="fileType">The file extension to filter by (null for any).</param>
        /// <param name="sizeRange">A tuple with minimum and maximum file size in bytes (null for any).</param>
        /// <param name="dateRange">A tuple with start and end dates (null for any).</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of matching file paths.</returns>
        public async Task<List<string>> SearchWithFiltersAsync(string directory, string query, string? fileType = null, Tuple<long, long>? sizeRange = null, Tuple<DateTime, DateTime>? dateRange = null)
        {
            return await Task.Run(() => SearchWithFilters(directory, query, fileType, sizeRange, dateRange));
        }
        
        /// <summary>
        /// Asynchronously searches for content within PDF and DOCX files.
        /// </summary>
        /// <param name="directory">The directory to search in.</param>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">The maximum number of results to return.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of matching file paths.</returns>
        public async Task<List<string>> SearchFileContentAsync(string directory, string query, int maxResults = 10)
        {
            return await Task.Run(() => SearchFileContent(directory, query, maxResults));
        }
    }

    /// <summary>
    /// Interface for fuzzy string matching.
    /// </summary>
    public interface IFuzzyMatcher
    {
        /// <summary>
        /// Calculates a partial ratio between two strings.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>A similarity score between 0 and 100.</returns>
        int PartialRatio(string s1, string s2);
    }

    /// <summary>
    /// Implementation of the fuzzy matcher using FuzzySharp (C# port of TheFuzz).
    /// </summary>
    public class FuzzySharpMatcher : IFuzzyMatcher
    {
        public int PartialRatio(string s1, string s2)
        {
            return FuzzySharp.Fuzz.PartialRatio(s1, s2);
        }
    }
}