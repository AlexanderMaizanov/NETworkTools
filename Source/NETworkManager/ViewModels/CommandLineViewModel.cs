﻿using System;
using System.Linq;
using System.Windows.Input;
using NETworkManager.Documentation;
using NETworkManager.Models;
using NETworkManager.Settings;
using NETworkManager.Utilities;

namespace NETworkManager.ViewModels;

public class CommandLineViewModel : ViewModelBase1
{
    #region Constructor, load settings

    public CommandLineViewModel()
    {
        if (!string.IsNullOrEmpty(CommandLineManager.Current.WrongParameter))
        {
            WrongParameter = CommandLineManager.Current.WrongParameter;
            DisplayWrongParameter = true;
        }

        ParameterHelp = CommandLineManager.ParameterHelp;
        ParameterResetSettings = CommandLineManager.ParameterResetSettings;
        ParameterApplication =
            CommandLineManager.GetParameterWithSplitIdentifier(CommandLineManager.ParameterApplication);
        ParameterApplicationValues = string.Join(", ",
            Enum.GetValues(typeof(ApplicationName)).Cast<ApplicationName>().ToList());
    }

    #endregion

    #region Variables

    private bool _displayWrongParameter;

    public bool DisplayWrongParameter
    {
        get => _displayWrongParameter;
        set
        {
            if (value == _displayWrongParameter)
                return;

            _displayWrongParameter = value;
            OnPropertyChanged();
        }
    }

    private string _wrongParameter;

    public string WrongParameter
    {
        get => _wrongParameter;
        set
        {
            if (value == _wrongParameter)
                return;

            _wrongParameter = value;
            OnPropertyChanged();
        }
    }

    private string _parameterHelp;

    public string ParameterHelp
    {
        get => _parameterHelp;
        set
        {
            if (value == _parameterHelp)
                return;

            _parameterHelp = value;
            OnPropertyChanged();
        }
    }

    private string _parameterResetSettings;

    public string ParameterResetSettings
    {
        get => _parameterResetSettings;
        set
        {
            if (value == _parameterResetSettings)
                return;

            _parameterResetSettings = value;
            OnPropertyChanged();
        }
    }

    private string _parameterApplication;

    public string ParameterApplication
    {
        get => _parameterApplication;
        set
        {
            if (value == _parameterApplication)
                return;

            _parameterApplication = value;
            OnPropertyChanged();
        }
    }

    private string _parameterApplicationValues;

    public string ParameterApplicationValues
    {
        get => _parameterApplicationValues;
        set
        {
            if (value == _parameterApplicationValues)
                return;

            _parameterApplicationValues = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region ICommand & Actions

    public ICommand OpenDocumentationCommand
    {
        get { return new RelayCommand(_ => OpenDocumentationAction()); }
    }

    private void OpenDocumentationAction()
    {
        DocumentationManager.OpenDocumentation(DocumentationIdentifier.CommandLineArguments);
    }

    #endregion
}