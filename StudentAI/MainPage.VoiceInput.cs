using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

#if WINDOWS
using Windows.Media.SpeechRecognition;
#endif

namespace StudentAI;

public partial class MainPage
{
    private ToolbarItem? microphoneToolbarItem;

    private CancellationTokenSource? voiceInputCancellation;

    private bool voiceInputInstalled;

    private void InitializeVoiceInput()
    {
        if (voiceInputInstalled)
            return;

        voiceInputInstalled = true;

        microphoneToolbarItem = new ToolbarItem
        {
            Text = "MIC",
            Order = ToolbarItemOrder.Primary,
            Priority = 0
        };

        microphoneToolbarItem.Clicked += OnMicrophoneClicked;

        ToolbarItems.Add(microphoneToolbarItem);
    }

    private void CancelVoiceInput()
    {
        CancellationTokenSource? cancellation =
            voiceInputCancellation;

        if (cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Recognition has already finished.
        }
    }

    private async void OnMicrophoneClicked(
        object? sender,
        EventArgs e)
    {
        // Clicking the microphone again cancels recognition.
        if (voiceInputCancellation is not null)
        {
            CancelVoiceInput();
            return;
        }

        if (!initialized ||
            !visible ||
            busy ||
            toolsOpen)
        {
            return;
        }

#if WINDOWS
        StopSpeech();

        string originalText =
            MessageEntry.Text?.Trim() ?? "";

        var cancellation =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(60));

        voiceInputCancellation = cancellation;

        busy = true;

        SendButton.IsEnabled = false;
        ToolsButton.IsEnabled = false;
        MessageEntry.IsEnabled = false;
        ModePicker.IsEnabled = false;

        if (microphoneToolbarItem is not null)
        {
            microphoneToolbarItem.Text = "STOP MIC";
        }

        SetMode(
            OrbMode.Idle,
            "Preparing microphone...");

        SpeechRecognizer? recognizer = null;

        try
        {
            recognizer = new SpeechRecognizer();

            recognizer.Timeouts.InitialSilenceTimeout =
                TimeSpan.FromSeconds(10);

            recognizer.Timeouts.EndSilenceTimeout =
                TimeSpan.FromSeconds(1.5);

            recognizer.Timeouts.BabbleTimeout =
                TimeSpan.FromSeconds(30);

            // With no custom constraints, Windows uses
            // its default dictation grammar.
            SpeechRecognitionCompilationResult compilation =
                await recognizer.CompileConstraintsAsync();

            cancellation.Token.ThrowIfCancellationRequested();

            if (compilation.Status !=
                SpeechRecognitionResultStatus.Success)
            {
                throw new InvalidOperationException(
                    "Windows could not prepare speech recognition. " +
                    "Status: " + compilation.Status +
                    "\n\nCheck your installed speech language " +
                    "and Windows speech settings.");
            }

            SetMode(
                OrbMode.Idle,
                "Listening... click STOP MIC to cancel");

            SpeechRecognitionResult result =
                await recognizer
                    .RecognizeAsync()
                    .AsTask(cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();

            if (result.Status ==
                SpeechRecognitionResultStatus.Success)
            {
                string recognizedText =
                    result.Text?.Trim() ?? "";

                if (recognizedText.Length == 0)
                {
                    if (visible)
                    {
                        await DisplayAlertAsync(
                            "No words recognized",
                            "Try again and speak a short sentence.",
                            "OK");
                    }

                    return;
                }

                // Do not automatically send or execute the result.
                // Preserve any text already in the message box.
                MessageEntry.Text =
                    originalText.Length == 0
                        ? recognizedText
                        : originalText + " " + recognizedText;
            }
            else
            {
                if (visible)
                {
                    await DisplayAlertAsync(
                        "Speech was not recognized",
                        "Windows returned: " + result.Status +
                        "\n\nTry again, check your microphone, " +
                        "or speak a shorter request.",
                        "OK");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if STOP MIC is clicked, the page closes,
            // or the 60-second recognition timeout expires.
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "Voice input error: " + ex);

            if (visible)
            {
                await DisplayAlertAsync(
                    "Voice input could not start",
                    ex.Message +
                    "\n\nCheck:\n" +
                    "• Microphone access in Windows Settings.\n" +
                    "• Online speech recognition settings.\n" +
                    "• Your installed speech language.\n" +
                    "• The microphone capability in Package.appxmanifest.",
                    "OK");
            }
        }
        finally
        {
            try
            {
                recognizer?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Speech recognizer cleanup: " + ex.Message);
            }

            if (ReferenceEquals(
                voiceInputCancellation,
                cancellation))
            {
                voiceInputCancellation = null;
            }

            cancellation.Dispose();

            busy = false;

            SendButton.IsEnabled = true;
            ToolsButton.IsEnabled = true;
            MessageEntry.IsEnabled = true;
            ModePicker.IsEnabled = true;

            if (microphoneToolbarItem is not null)
            {
                microphoneToolbarItem.Text = "MIC";
            }

            SetMode(OrbMode.Idle);
        }
#else
        await DisplayAlertAsync(
            "Windows-only feature",
            "This voice-input add-on is configured for Windows.",
            "OK");
#endif
    }
}