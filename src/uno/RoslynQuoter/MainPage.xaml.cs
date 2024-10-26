using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using RoslynQuoter;
using Windows.Web.Http;
using QuoterWeb;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RoslynQuoterApp;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    private const string SDKResourcePrefix = "mono_sdk";
    private readonly NodeKind[] _kinds;

    public MainPage()
    {
        this.InitializeComponent();

        _kinds = new[] {
            NodeKind.CompilationUnit,
            NodeKind.Statement,
            NodeKind.Expression
        };

        comboParseAs.ItemsSource = _kinds;
        comboParseAs.SelectedIndex = 0;
    }

    private void OnCodeChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void OnGenerateCode(object sender, RoutedEventArgs e)
    {
        result.Text = GenerateCode();
    }

    private async void OnGenerateLinqPadCode(object sender, RoutedEventArgs e)
    {
        var linqpadFile = $@"<Query Kind=""Expression"">
				  <NuGetReference>Microsoft.CodeAnalysis.Compilers</NuGetReference>
				  <NuGetReference>Microsoft.CodeAnalysis.CSharp</NuGetReference>
				  <Namespace>static Microsoft.CodeAnalysis.CSharp.SyntaxFactory</Namespace>
				  <Namespace>Microsoft.CodeAnalysis.CSharp.Syntax</Namespace>
				  <Namespace>Microsoft.CodeAnalysis.CSharp</Namespace>
				  <Namespace>Microsoft.CodeAnalysis</Namespace>
				</Query>

				{GenerateCode()}
			";


        try
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();

            savePicker.SuggestedStartLocation =
                Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("LINQPad File", new List<string>() { ".linq" });
            savePicker.SuggestedFileName = "Quoter";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();

            // Prevent updates to the remote version of the file until
            // we finish making changes and call CompleteUpdatesAsync.
            Windows.Storage.CachedFileManager.DeferUpdates(file);

            // write to file
            await Windows.Storage.FileIO.WriteTextAsync(file, linqpadFile);

            // Let Windows know that we're finished changing the file so
            // the other app can update the remote version of the file.
            // Completing updates may require Windows to ask for user input.
            Windows.Storage.Provider.FileUpdateStatus status =
                await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private string GenerateCode()
    {
        string responseText = "";

        try
        {
            string sourceText = inputCode.Text;

            var nodeKind = _kinds[comboParseAs.SelectedIndex];
            bool openCurlyOnNewLine = checkBoxOpenParenthesis.IsChecked ?? false;
            bool closeCurlyOnNewLine = checkBoxCloseParenthesis.IsChecked ?? false;
            bool preserveOriginalWhitespace = checkBoxPreserveWhiteSpace.IsChecked ?? false;

            bool keepRedundantApiCalls = checkBoxKeepRedundant.IsChecked ?? false;

            bool avoidUsingStatic = checkBoxNoSyntaxFactory.IsChecked ?? false;


            if (string.IsNullOrEmpty(sourceText))
            {
                responseText = "Please specify the source text.";
            }
            else
            {
                ExtractSDK();

                var quoter = new Quoter
                {
                    OpenParenthesisOnNewLine = openCurlyOnNewLine,
                    ClosingParenthesisOnNewLine = closeCurlyOnNewLine,
                    UseDefaultFormatting = !preserveOriginalWhitespace,
                    RemoveRedundantModifyingCalls = !keepRedundantApiCalls,
                    ShortenCodeWithUsingStatic = !avoidUsingStatic
                };

                responseText = quoter.QuoteText(sourceText, nodeKind);

                if (readyToRun.IsChecked ?? false)
                {
                    responseText = ReadyToRunHelper.CreateReadyToRunCode(new QuoterRequestArgument
                    {
                        SourceText = sourceText,
                        NodeKind = nodeKind,
                        OpenCurlyOnNewLine = openCurlyOnNewLine,
                        CloseCurlyOnNewLine = closeCurlyOnNewLine,
                        PreserveOriginalWhitespace = preserveOriginalWhitespace,
                        KeepRedundantApiCalls = keepRedundantApiCalls,
                        AvoidUsingStatic = avoidUsingStatic,
                        ReadyToRun = true
                    }, responseText);
                }
            }

        }
        catch (Exception ex)
        {
            responseText = "Congratulations! You've found a bug in Quoter! Please open an issue " +
                "at https://github.com/KirillOsenkov/RoslynQuoter/issues/new and " +
                "paste the code you've typed above and this stack:";
            responseText += ex.ToString();
        }

        return responseText;
    }

    [Conditional("__WASM__")]
    private void ExtractSDK()
    {
        var sdkFiles = this.GetType().Assembly.GetManifestResourceNames().Where(f => f.Contains(SDKResourcePrefix));

        foreach (var sdkFile in sdkFiles)
        {
            var fileNameStart = sdkFile.IndexOf(SDKResourcePrefix) + (SDKResourcePrefix + ".").Length;
            var outputFile = sdkFile.Substring(fileNameStart);

            if (!File.Exists(outputFile))
            {
                using (var s = this.GetType().Assembly.GetManifestResourceStream(sdkFile)!)
                {
                    Console.WriteLine($"Writing {outputFile}");

                    using (var output = File.OpenWrite(outputFile))
                    {
                        s.CopyTo(output);
                    }
                }
            }
        }
    }

    private async void OnForkMe(object sender, TappedRoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/nventive/Uno.RoslynQuoter"));
    }
}
