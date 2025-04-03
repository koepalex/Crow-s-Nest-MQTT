using System.Dynamic;

using CrowsNestMqtt.Businesslogic.Commands;
using CrowsNestMqtt.Businesslogic.Configuration;

namespace CrowsNestMqtt.Businesslogic.Services;

/// <summary>
/// Defines the contract for a service that parses user input into commands or search terms.
/// </summary>
public interface ICommandParserService
{
    /// <summary>
    /// Parses the given input string.
    /// </summary>
    /// <param name="input">The user input string.</param>
    /// <param name="settingsData"></param>
    /// <returns>A <see cref="CommandResult"/> indicating success or failure,
    /// containing either a <see cref="ParsedCommand"/> or a search term.</returns>
    CommandResult ParseInput(string input, SettingsData settingsData);
}