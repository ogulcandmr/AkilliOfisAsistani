using DevExpress.XtraEditors;
using System;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OfisAsistan.Services
{
    public class VoiceService
    {
        private SpeechRecognitionEngine _recognizer;
        private SpeechSynthesizer _synthesizer;
        private bool _isListening;
        public event EventHandler<string> VoiceCommandReceived;

        public VoiceService()
        {
            InitializeSpeechRecognition();
            InitializeSpeechSynthesis();
        }

        private void InitializeSpeechRecognition()
        {
            try
            {
                _recognizer = new SpeechRecognitionEngine();
                var grammar = CreateGrammar();
                _recognizer.LoadGrammar(grammar);
                _recognizer.SetInputToDefaultAudioDevice();
                _recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
                _recognizer.SpeechRecognitionRejected += Recognizer_SpeechRecognitionRejected;
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Ses tanıma başlatılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Grammar CreateGrammar()
        {
            // Temel komutlar için grammar
            var commands = new Choices();
            commands.Add("görev ata");
            commands.Add("rapor göster");
            commands.Add("bitmeyen işler");
            commands.Add("bu hafta");
            commands.Add("bugün");
            commands.Add("tamamlandı");
            commands.Add("iptal");

            var grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(commands);
            grammarBuilder.AppendDictation(); // Serbest konuşma için

            return new Grammar(grammarBuilder);
        }

        private void InitializeSpeechSynthesis()
        {
            _synthesizer = new SpeechSynthesizer();
            _synthesizer.SetOutputToDefaultAudioDevice();
            _synthesizer.Rate = 0; // Normal hız
            _synthesizer.Volume = 100;
        }

        public void StartListening()
        {
            if (_isListening) return;

            try
            {
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                _isListening = true;
                Speak("Dinliyorum, komutunuzu söyleyin.");
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Dinleme başlatılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void StopListening()
        {
            if (!_isListening) return;

            try
            {
                _recognizer.RecognizeAsyncStop();
                _isListening = false;
                Speak("Dinleme durduruldu.");
            }
            catch (Exception ex)
            {
                XtraMessageBox.Show($"Dinleme durdurulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Speak(string text)
        {
            try
            {
                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speak Error: {ex.Message}");
            }
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            var command = e.Result.Text;
            System.Diagnostics.Debug.WriteLine($"Tanınan komut: {command}");
            VoiceCommandReceived?.Invoke(this, command);
        }

        private void Recognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Komut tanınamadı.");
        }

        public void Dispose()
        {
            StopListening();
            _recognizer?.Dispose();
            _synthesizer?.Dispose();
        }
    }
}

