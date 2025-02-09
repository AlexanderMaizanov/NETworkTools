﻿using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Octokit;

namespace NETworkManager.Update;

/// <summary>
///     Updater to check if a new program version is available.
/// </summary>
public sealed class Updater
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Updater));

    #region Methods
    /// <summary>
    ///     Checks on GitHub whether a new version of the program is available
    /// </summary>
    /// <param name="userName">GitHub username like "BornToBeRoot".</param>
    /// <param name="projectName">GitHub repository like "NETworkManager".</param>
    /// <param name="currentVersion">Version like 1.2.0.0.</param>
    /// <param name="includePreRelease">Include pre-release versions</param>
    public void CheckOnGitHub(string userName, string projectName, Version currentVersion, bool includePreRelease)
    {
        Task.Run(() =>
        {
            try
            {
                Log.Info("Checking for new version on GitHub...");

                // Create GitHub client
                var client = new GitHubClient(new ProductHeaderValue(userName + "_" + projectName));

                // Get latest or pre-release version
                var release = includePreRelease
                    ? client.Repository.Release.GetAll(userName, projectName).Result[0]
                    : client.Repository.Release.GetLatest(userName, projectName).Result;

                // Compare versions (tag=2021.2.15.0, version=2021.2.15.0)
                if (new Version(release.TagName) > currentVersion)
                {
                    Log.Info($"Version \"{release.TagName}\" is available!");
                    OnUpdateAvailable(new UpdateAvailableArgs(release));
                }
                else
                {
                    Log.Info("No newer version found!");
                    OnNoUpdateAvailable();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error while checking for new version on GitHub!", ex);
                OnError();
            }
        });
    }

    /// <summary>
    ///     Checks asyncronously on GitHub whether a new version of the program is available
    /// </summary>
    /// <param name="userName">GitHub username like "BornToBeRoot".</param>
    /// <param name="projectName">GitHub repository like "NETworkManager".</param>
    /// <param name="currentVersion">Version like 1.2.0.0.</param>
    /// <param name="includePreRelease">Include pre-release versions</param>
    public async Task CheckOnGitHubAsync(string userName, string projectName, Version currentVersion, bool includePreRelease, CancellationToken cancellationToken)
    {
        try
        {
            Log.Info("Checking for new version on GitHub...");

            // Create GitHub client
            var client = new GitHubClient(new ProductHeaderValue(userName + "_" + projectName));

            // Get latest or pre-release version
            var release = includePreRelease
                ? (await client.Repository.Release.GetAll(userName, projectName).WaitAsync(cancellationToken))[0]
                : await client.Repository.Release.GetLatest(userName, projectName).WaitAsync(cancellationToken);

            // Compare versions (tag=2021.2.15.0, version=2021.2.15.0)
            if (new Version(release.TagName) > currentVersion)
            {
                Log.Info($"Version \"{release.TagName}\" is available!");
                OnUpdateAvailable(new UpdateAvailableArgs(release));
            }
            else
            {
                Log.Info("No newer version found!");
                OnNoUpdateAvailable();
            }
        }
        catch (Exception ex)
        {
            Log.Error("Error while checking for new version on GitHub!", ex);
            OnError();
        }
    }

    #endregion

    #region Events

    /// <summary>
    ///     Is triggered when update check is complete and an update is available.
    /// </summary>
    public event EventHandler<UpdateAvailableArgs> UpdateAvailable;

    /// <summary>
    ///     Triggers the <see cref="UpdateAvailable" /> event.
    /// </summary>
    /// <param name="e">Passes <see cref="UpdateAvailableArgs" /> to the event.</param>
    private void OnUpdateAvailable(UpdateAvailableArgs e)
    {
        UpdateAvailable?.Invoke(this, e);
    }

    /// <summary>
    ///     Is triggered when update check is complete and no update is available.
    /// </summary>
    public event EventHandler NoUpdateAvailable;

    /// <summary>
    ///     Triggers the <see cref="NoUpdateAvailable" /> event.
    /// </summary>
    private void OnNoUpdateAvailable()
    {
        NoUpdateAvailable?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Is triggered when an error occurs during the update check.
    /// </summary>
    public event EventHandler Error;

    /// <summary>
    ///     Triggers the <see cref="Error" /> event.
    /// </summary>
    private void OnError()
    {
        Error?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}