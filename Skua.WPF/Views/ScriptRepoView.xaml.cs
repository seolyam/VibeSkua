using Skua.Core.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Skua.WPF.Views;

/// <summary>
/// Interaction logic for ScriptRepoView.xaml
/// </summary>
public partial class ScriptRepoView : UserControl
{
    private ICollectionView? _collectionView;

    public ScriptRepoView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private readonly object _syncLock = new();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ScriptRepoViewModel vm)
        {
            BindingOperations.EnableCollectionSynchronization(vm.Scripts, _syncLock);
            _collectionView = CollectionViewSource.GetDefaultView(vm.Scripts);
        }
    }

    private bool Search(object obj)
    {
        bool flag = false;
        string searchScript = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchScript))
            return true;

        ScriptInfoViewModel? script = (ScriptInfoViewModel)obj;
        if (script is null)
            return false;

        string scriptName = script.Info.Name?.ToLower() ?? string.Empty;
        if (KMPSearch(scriptName, searchScript))
            flag = true;

        if (!flag && script.Info.FileName != null)
        {
            if (KMPSearch(script.Info.FileName.ToLower(), searchScript))
                flag = true;
        }

        if (!flag && script.Info.FilePath != null)
        {
            if (KMPSearch(script.Info.FilePath.ToLower(), searchScript))
                flag = true;
        }
        
        if (!flag && script.Info.Description != null)
        {
            if (KMPSearch(script.Info.Description.ToLower(), searchScript))
                flag = true;
        }

        if (!flag)
        {
            foreach (string tag in script.InfoTags)
            {
                if (KMPSearch(tag, searchScript))
                {
                    flag = true;
                    break;
                }
            }
        }

        return flag;
    }

    private System.Threading.Timer? _debounceTimer;

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_collectionView is null)
            return;

        _debounceTimer?.Change(System.Threading.Timeout.Infinite, 0);
        _debounceTimer = new System.Threading.Timer((state) =>
        {
            Dispatcher.Invoke(() =>
            {
                _collectionView.Filter = Search;
                _collectionView.Refresh();
            });
        }, null, 250, System.Threading.Timeout.Infinite);
    }

    private bool KMPSearch(string text, string pattern)
    {
        int n = text.Length;
        int m = pattern.Length;
        int[] lps = new int[m];
        int j = 0; // index for pattern[]

        // Preprocess the pattern (calculate lps[] array)
        ComputeLPSArray(pattern, m, lps);

        int i = 0;  // index for text[]
        while (i < n)
        {
            if (pattern[j] == text[i])
            {
                j++;
                i++;
            }

            if (j == m)
                return true;

            // mismatch after j matches
            else if (i < n && pattern[j] != text[i])
            {
                // Do not match lps[0..lps[j-1]] characters,
                // they will match anyway
                if (j != 0)
                    j = lps[j - 1];
                else
                    i++;
            }
        }
        return false;
    }

    private void ComputeLPSArray(string pattern, int m, int[] lps)
    {
        int len = 0;
        int i = 1;
        lps[0] = 0; // lps[0] is always 0

        // the loop calculates lps[i] for i = 1 to m-1
        while (i < m)
        {
            if (pattern[i] == pattern[len])
            {
                len++;
                lps[i] = len;
                i++;
            }
            else // (pat[i] != pat[len])
            {
                if (len != 0)
                {
                    len = lps[len - 1];

                    // Also, note that we do not increment i here
                }
                else  // if (len == 0)
                {
                    lps[i] = 0;
                    i++;
                }
            }
        }
    }
}