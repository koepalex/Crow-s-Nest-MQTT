using CrowsNestMqtt.UI.Services;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class FileAutoCompleteServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileAutoCompleteService _service;

    public FileAutoCompleteServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "CrowsNestMqtt_Tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _service = new FileAutoCompleteService(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    private void CreateFile(string relativePath, string content = "test")
    {
        var fullPath = Path.Combine(_testDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
    }

    private void CreateDir(string relativePath)
    {
        Directory.CreateDirectory(Path.Combine(_testDir, relativePath));
    }

    [Fact]
    public void GetSuggestions_EmptyInput_ReturnsRootContents()
    {
        // Arrange
        CreateDir("docs");
        CreateFile("readme.txt");
        CreateFile("config.json");

        // Act
        var results = _service.GetSuggestions("");

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, s => s.DisplayName == "docs" + Path.DirectorySeparatorChar && s.IsDirectory);
        Assert.Contains(results, s => s.DisplayName == "readme.txt" && !s.IsDirectory);
        Assert.Contains(results, s => s.DisplayName == "config.json" && !s.IsDirectory);
    }

    [Fact]
    public void GetSuggestions_NullInput_ReturnsRootContents()
    {
        // Arrange
        CreateFile("file1.txt");

        // Act
        var results = _service.GetSuggestions(null!);

        // Assert
        Assert.Single(results);
        Assert.Equal("file1.txt", results[0].DisplayName);
    }

    [Fact]
    public void GetSuggestions_PartialFileName_ReturnsMatchingFiles()
    {
        // Arrange
        CreateFile("readme.md");
        CreateFile("report.txt");
        CreateFile("config.json");

        // Act
        var results = _service.GetSuggestions("re");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, s => s.DisplayName == "readme.md");
        Assert.Contains(results, s => s.DisplayName == "report.txt");
        Assert.DoesNotContain(results, s => s.DisplayName == "config.json");
    }

    [Fact]
    public void GetSuggestions_DirectoryPath_ReturnsDirectoryContents()
    {
        // Arrange
        CreateDir("subdir");
        CreateFile(Path.Combine("subdir", "alpha.txt"));
        CreateFile(Path.Combine("subdir", "beta.txt"));
        CreateDir(Path.Combine("subdir", "nested"));

        // Act
        var results = _service.GetSuggestions("subdir");

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, s => s.DisplayName == "nested" + Path.DirectorySeparatorChar && s.IsDirectory);
        Assert.Contains(results, s => s.DisplayName == "alpha.txt" && !s.IsDirectory);
        Assert.Contains(results, s => s.DisplayName == "beta.txt" && !s.IsDirectory);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".idea")]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("__pycache__")]
    [InlineData(".svn")]
    public void GetSuggestions_ExcludedDirectories_AreFiltered(string excludedDir)
    {
        // Arrange
        CreateDir(excludedDir);
        CreateDir("src");

        // Act
        var results = _service.GetSuggestions("");

        // Assert
        Assert.Single(results);
        Assert.Equal("src" + Path.DirectorySeparatorChar, results[0].DisplayName);
        Assert.DoesNotContain(results, s => s.DisplayName.TrimEnd(Path.DirectorySeparatorChar) == excludedDir);
    }

    [Fact]
    public void GetSuggestions_NonExistentPath_ReturnsEmptyList()
    {
        // Arrange — no directories or files created

        // Act
        var results = _service.GetSuggestions(Path.Combine("nonexistent", "path"));

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void GetSuggestions_MaxResults_LimitsOutput()
    {
        // Arrange
        for (var i = 0; i < 30; i++)
            CreateFile($"file{i:D2}.txt");

        // Act
        var results = _service.GetSuggestions("", maxResults: 5);

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void GetSuggestions_CaseInsensitive_MatchesBothCases()
    {
        // Arrange
        CreateFile("ReadMe.md");
        CreateFile("README.txt");
        CreateFile("other.txt");

        // Act
        var results = _service.GetSuggestions("readme");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, s => s.DisplayName == "ReadMe.md");
        Assert.Contains(results, s => s.DisplayName == "README.txt");
    }

    [Fact]
    public void GetSuggestions_DirectoriesFirst_ThenFiles()
    {
        // Arrange
        CreateFile("alpha.txt");
        CreateDir("beta");
        CreateFile("gamma.txt");
        CreateDir("delta");

        // Act
        var results = _service.GetSuggestions("");

        // Assert
        Assert.Equal(4, results.Count);
        // Directories should come before files
        var dirResults = results.TakeWhile(s => s.IsDirectory).ToList();
        var fileResults = results.SkipWhile(s => s.IsDirectory).ToList();
        Assert.Equal(2, dirResults.Count);
        Assert.All(fileResults, s => Assert.False(s.IsDirectory));
    }

    [Fact]
    public void GetSuggestions_ReturnsSizeAndExtension_ForFiles()
    {
        // Arrange
        var content = "Hello, World!";
        CreateFile("data.json", content);

        // Act
        var results = _service.GetSuggestions("data");

        // Assert
        var suggestion = Assert.Single(results);
        Assert.False(suggestion.IsDirectory);
        Assert.Equal(".json", suggestion.Extension);
        Assert.Equal(content.Length, suggestion.SizeBytes);
    }

    [Fact]
    public void GetSuggestions_DirectoryTrailingSeparator_InDisplayName()
    {
        // Arrange
        CreateDir("myFolder");

        // Act
        var results = _service.GetSuggestions("");

        // Assert
        var suggestion = Assert.Single(results);
        Assert.True(suggestion.IsDirectory);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), suggestion.DisplayName);
        Assert.Equal("myFolder" + Path.DirectorySeparatorChar, suggestion.DisplayName);
    }

    [Fact]
    public void GetSuggestions_RelativePaths_ResolvedFromBasePath()
    {
        // Arrange
        CreateDir("sub");
        CreateFile(Path.Combine("sub", "inside.txt"));

        // Act
        var results = _service.GetSuggestions("sub");

        // Assert
        var fileSuggestion = results.First(s => !s.IsDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(_testDir, "sub", "inside.txt")), fileSuggestion.Path);
    }

    [Fact]
    public void BasePath_ReflectsConstructorArgument()
    {
        Assert.Equal(_testDir, _service.BasePath);
    }

    [Fact]
    public void GetSuggestions_RootedAbsolutePath_ReturnsEmptyList()
    {
        // Arrange — create a file at an absolute path inside the base dir.
        CreateFile("absolute.json");
        var absolutePath = Path.Combine(_testDir, "absolute.json");

        // Act — rooted partial paths should never produce completion suggestions.
        var results = _service.GetSuggestions(absolutePath);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void GetSuggestions_SuggestionPath_IsAbsolute()
    {
        // Arrange
        CreateFile("foo.json");

        // Act
        var results = _service.GetSuggestions("foo");

        // Assert
        var suggestion = Assert.Single(results);
        Assert.True(Path.IsPathRooted(suggestion.Path), $"Expected absolute path, got '{suggestion.Path}'");
        Assert.Equal(Path.GetFullPath(Path.Combine(_testDir, "foo.json")), suggestion.Path);
    }

    [Fact]
    public void GetSuggestions_NestedDirectory_ReturnsContents()
    {
        // Arrange
        CreateDir(Path.Combine("level1", "level2"));
        CreateFile(Path.Combine("level1", "level2", "deep.txt"));
        CreateDir(Path.Combine("level1", "level2", "level3"));

        // Act
        var results = _service.GetSuggestions(Path.Combine("level1", "level2"));

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, s => s.DisplayName == "level3" + Path.DirectorySeparatorChar && s.IsDirectory);
        Assert.Contains(results, s => s.DisplayName == "deep.txt" && !s.IsDirectory);
    }

    [Fact]
    public void GetSuggestions_DirectorySuggestion_HasNullSizeAndExtension()
    {
        // Arrange
        CreateDir("myDir");

        // Act
        var results = _service.GetSuggestions("");

        // Assert
        var suggestion = Assert.Single(results);
        Assert.True(suggestion.IsDirectory);
        Assert.Null(suggestion.SizeBytes);
        Assert.Null(suggestion.Extension);
    }

    [Fact]
    public void GetSuggestions_PartialDirectoryName_MatchesDirectories()
    {
        // Arrange
        CreateDir("src");
        CreateDir("specs");
        CreateDir("tools");

        // Act
        var results = _service.GetSuggestions("s");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, s => Assert.True(s.IsDirectory));
        Assert.Contains(results, s => s.DisplayName == "src" + Path.DirectorySeparatorChar);
        Assert.Contains(results, s => s.DisplayName == "specs" + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void GetSuggestions_ExcludedDirectories_NotMatchedByPrefix()
    {
        // Arrange
        CreateDir(".gitignore_dir");
        CreateDir("bindings");

        // Act — searching with prefixes that should match non-excluded dirs
        var gitResults = _service.GetSuggestions(".giti");
        var binResults = _service.GetSuggestions("bind");

        // Assert — .gitignore_dir and bindings match their prefixes
        Assert.Single(gitResults);
        Assert.Equal(".gitignore_dir" + Path.DirectorySeparatorChar, gitResults[0].DisplayName);

        Assert.Single(binResults);
        Assert.Equal("bindings" + Path.DirectorySeparatorChar, binResults[0].DisplayName);
    }

    [Fact]
    public void GetSuggestions_MaxResultsWithMixedContent_RespectsLimit()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
            CreateDir($"dir{i:D2}");
        for (var i = 0; i < 5; i++)
            CreateFile($"file{i:D2}.txt");

        // Act
        var results = _service.GetSuggestions("", maxResults: 3);

        // Assert
        Assert.Equal(3, results.Count);
        // With maxResults=3, directories are listed first so all 3 should be directories
        Assert.All(results, s => Assert.True(s.IsDirectory));
    }

    [Fact]
    public void GetSuggestions_WhitespaceInput_ReturnsRootContents()
    {
        // Arrange
        CreateFile("data.txt");

        // Act
        var results = _service.GetSuggestions("   ");

        // Assert
        Assert.Single(results);
        Assert.Equal("data.txt", results[0].DisplayName);
    }

    [Fact]
    public void GetSuggestions_DirectoryWithTrailingSeparator_ReturnsContents()
    {
        // Arrange
        CreateDir("mydir");
        CreateFile(Path.Combine("mydir", "file.txt"));

        // Act
        var results = _service.GetSuggestions("mydir" + Path.DirectorySeparatorChar);

        // Assert
        Assert.Single(results);
        Assert.Equal("file.txt", results[0].DisplayName);
    }
}
