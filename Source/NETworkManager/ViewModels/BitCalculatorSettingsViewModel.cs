using System;
using System.Collections.Generic;
using System.Linq;
using NETworkManager.Models.Network;
using NETworkManager.Settings;

namespace NETworkManager.ViewModels;

public class BitCalculatorSettingsViewModel : ViewModelBase
{
    #region Variables

    private readonly bool _isLoading;

    public List<BitCalculatorNotation> Notations { get; private set; }

    private BitCalculatorNotation _notation;

    public BitCalculatorNotation Notation
    {
        get => _notation;
        set
        {
            if (value == _notation)
                return;


            if (!_isLoading)
                SettingsManager.Current.BitCalculator_Notation = value;


            _notation = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Constructor, load settings

    public BitCalculatorSettingsViewModel()
    {
        _isLoading = true;

        LoadSettings();

        _isLoading = false;
    }

    private void LoadSettings()
    {
        Notations = Enum.GetValues(typeof(BitCalculatorNotation)).Cast<BitCalculatorNotation>()
            .OrderBy(x => x.ToString()).ToList();
        Notation = Notations.First(x => x == SettingsManager.Current.BitCalculator_Notation);
    }

    #endregion
}