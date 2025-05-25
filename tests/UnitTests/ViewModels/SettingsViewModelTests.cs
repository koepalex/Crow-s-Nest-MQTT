using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Configuration;
using System.IO;
using System;
using System.Text.Json; // Required for direct JsonSerializer usage if needed

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class SettingsViewModelTests : IDisposable
    {
        private readonly string _testSettingsFilePath;

        public SettingsViewModelTests()
        {
            // Use a unique temp file for each test run to avoid interference
            _testSettingsFilePath = Path.Combine(Path.GetTempPath(), $"test_settings_{Guid.NewGuid()}.json");
            
            // Override the static settings file path in SettingsViewModel for testing
            // This is a common approach but be mindful if tests run in parallel without proper isolation.
            // For more robust tests, consider injecting the file path or using a mocking framework.
            var fieldInfo = typeof(SettingsViewModel).GetField("_settingsFilePath", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            fieldInfo?.SetValue(null, _testSettingsFilePath);
        }

        [Fact]
        public void FromAndInto_WithUsernamePasswordAuth_ShouldPreserveData()
        {
            // Arrange
            var viewModel = new SettingsViewModel();
            var auth = new UsernamePasswordAuthenticationMode("user1", "pass1");
            var originalSettingsData = new SettingsData(
                Hostname: "test.com",
                Port: 1884,
                ClientId: "client123",
                KeepAliveIntervalSeconds: 30,
                CleanSession: false,
                SessionExpiryIntervalSeconds: 3600,
                AuthMode: auth,
                ExportFormat: BusinessLogic.Exporter.ExportTypes.txt,
                ExportPath: "/export/path"
            );

            // Act
            viewModel.From(originalSettingsData);
            var newSettingsData = viewModel.Into();

            // Assert
            Assert.Equal(originalSettingsData.Hostname, newSettingsData.Hostname);
            Assert.Equal(originalSettingsData.Port, newSettingsData.Port);
            Assert.Equal(originalSettingsData.ClientId, newSettingsData.ClientId);
            Assert.Equal(originalSettingsData.KeepAliveIntervalSeconds, newSettingsData.KeepAliveIntervalSeconds);
            Assert.Equal(originalSettingsData.CleanSession, newSettingsData.CleanSession);
            Assert.Equal(originalSettingsData.SessionExpiryIntervalSeconds, newSettingsData.SessionExpiryIntervalSeconds);
            Assert.Equal(originalSettingsData.ExportFormat, newSettingsData.ExportFormat);
            Assert.Equal(originalSettingsData.ExportPath, newSettingsData.ExportPath);
            
            Assert.IsType<UsernamePasswordAuthenticationMode>(newSettingsData.AuthMode);
            var newAuth = Assert.IsType<UsernamePasswordAuthenticationMode>(newSettingsData.AuthMode);
            Assert.Equal("user1", newAuth.Username);
            Assert.Equal("pass1", newAuth.Password);

            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.SelectedAuthMode);
            Assert.Equal("user1", viewModel.AuthUsername);
            Assert.Equal("pass1", viewModel.AuthPassword);
        }

        [Fact]
        public void FromAndInto_WithAnonymousAuth_ShouldPreserveData()
        {
            // Arrange
            var viewModel = new SettingsViewModel();
            var auth = new AnonymousAuthenticationMode();
            var originalSettingsData = new SettingsData(
                Hostname: "anon.com",
                Port: 1885,
                AuthMode: auth
            );

            // Act
            viewModel.From(originalSettingsData);
            var newSettingsData = viewModel.Into();

            // Assert
            Assert.Equal(originalSettingsData.Hostname, newSettingsData.Hostname);
            Assert.Equal(originalSettingsData.Port, newSettingsData.Port);
            Assert.IsType<AnonymousAuthenticationMode>(newSettingsData.AuthMode);
            Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, viewModel.SelectedAuthMode);
            Assert.Empty(viewModel.AuthUsername);
            Assert.Empty(viewModel.AuthPassword);
        }

        [Fact]
        public void IsUsernamePasswordSelected_ShouldReflectSelectedAuthMode()
        {
            // Arrange
            var viewModel = new SettingsViewModel();

            // Act & Assert for Anonymous
            viewModel.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
            Assert.False(viewModel.IsUsernamePasswordSelected);

            // Act & Assert for UsernamePassword
            viewModel.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
            Assert.True(viewModel.IsUsernamePasswordSelected);
        }

        [Fact]
        public void SaveAndLoadSettings_WithUsernamePasswordAuth_ShouldPersistAndLoad()
        {
            // Arrange - Save
            var saveViewModel = new SettingsViewModel
            {
                Hostname = "savehost.com",
                Port = 1993,
                SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword,
                AuthUsername = "saveuser",
                AuthPassword = "savepassword",
                ClientId = "saveClient",
                KeepAliveIntervalSeconds = 90,
                CleanSession = false,
                SessionExpiryIntervalSeconds = 7200,
                ExportFormat = BusinessLogic.Exporter.ExportTypes.json,
                ExportPath = "/save/path"
            };
            
            // Act - Save (SaveSettings is called by ReactiveUI through Observable.CombineLatest)
            // To directly test SaveSettings, we might need to expose it or trigger the observable manually.
            // For simplicity, we'll reflect to call it, or assume the auto-save has triggered if Throttle is short.
            // Let's directly call SaveSettings for this unit test's purpose.
            var saveMethod = typeof(SettingsViewModel).GetMethod("SaveSettings", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            saveMethod?.Invoke(saveViewModel, null);


            // Arrange - Load
            var loadViewModel = new SettingsViewModel(); // This will call LoadSettings in its constructor

            // Assert - Load
            Assert.Equal("savehost.com", loadViewModel.Hostname);
            Assert.Equal(1993, loadViewModel.Port);
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, loadViewModel.SelectedAuthMode);
            Assert.Equal("saveuser", loadViewModel.AuthUsername);
            Assert.Equal("savepassword", loadViewModel.AuthPassword);
            Assert.Equal("saveClient", loadViewModel.ClientId);
            Assert.Equal(90, loadViewModel.KeepAliveIntervalSeconds);
            Assert.False(loadViewModel.CleanSession);
            Assert.Equal(7200u, loadViewModel.SessionExpiryIntervalSeconds);
            Assert.Equal(BusinessLogic.Exporter.ExportTypes.json, loadViewModel.ExportFormat);
            Assert.Equal("/save/path", loadViewModel.ExportPath);
            Assert.True(loadViewModel.IsUsernamePasswordSelected);
        }

        [Fact]
        public void SaveAndLoadSettings_WithAnonymousAuth_ShouldPersistAndLoad()
        {
            // Arrange - Save
            var saveViewModel = new SettingsViewModel
            {
                Hostname = "savehostanon.com",
                Port = 1994,
                SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous
            };
            var saveMethod = typeof(SettingsViewModel).GetMethod("SaveSettings", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            saveMethod?.Invoke(saveViewModel, null);

            // Arrange - Load
            var loadViewModel = new SettingsViewModel();

            // Assert - Load
            Assert.Equal("savehostanon.com", loadViewModel.Hostname);
            Assert.Equal(1994, loadViewModel.Port);
            Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, loadViewModel.SelectedAuthMode);
            Assert.Empty(loadViewModel.AuthUsername);
            Assert.Empty(loadViewModel.AuthPassword);
            Assert.False(loadViewModel.IsUsernamePasswordSelected);
        }
        
        [Fact]
        public void LoadSettings_NonExistentFile_ShouldUseDefaults()
        {
            // Arrange: Ensure the file does not exist for this specific test
            if (File.Exists(_testSettingsFilePath))
            {
                File.Delete(_testSettingsFilePath);
            }
            
            var viewModel = new SettingsViewModel(); // Constructor calls LoadSettings

            // Assert: Check a few key default values
            Assert.Equal("localhost", viewModel.Hostname); // Default hostname
            Assert.Equal(1883, viewModel.Port);          // Default port
            Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, viewModel.SelectedAuthMode); // Default auth mode
            Assert.Empty(viewModel.AuthUsername);
            Assert.Empty(viewModel.AuthPassword);
             // Default export path is a specific folder, check if it's set
            string expectedDefaultExportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CrowsNestMqtt", 
                "exports");
            Assert.Equal(expectedDefaultExportPath, viewModel.ExportPath);
        }

        [Fact]
        public void LoadSettings_CorruptedFile_ShouldUseDefaultsAndLogError()
        {
            // Arrange: Create a corrupted JSON file
            File.WriteAllText(_testSettingsFilePath, "this is not valid json");
            
            // For checking logs, you'd typically inject a logger and mock it.
            // Here, we'll just ensure it doesn't crash and uses defaults.
            var viewModel = new SettingsViewModel();

            // Assert: Check defaults (similar to NonExistentFile test)
            Assert.Equal("localhost", viewModel.Hostname);
            Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, viewModel.SelectedAuthMode);
            // Add more assertions for other default values if necessary
        }


        public void Dispose()
        {
            // Clean up the test settings file after each test
            if (File.Exists(_testSettingsFilePath))
            {
                try
                {
                    File.Delete(_testSettingsFilePath);
                }
                catch (IOException ex)
                {
                    // Log or handle the exception if file deletion fails
                    Console.WriteLine($"Error deleting test settings file: {ex.Message}");
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
