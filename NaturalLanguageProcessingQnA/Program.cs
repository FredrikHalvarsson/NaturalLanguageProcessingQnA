using System;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Language.QuestionAnswering;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

namespace NaturalLanguageProcessingQnA
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            var endpoint = new Uri(configuration["Azure:Endpoint"]);
            var credential = new AzureKeyCredential(configuration["Azure:Key"]);
            var projectName = configuration["Azure:ProjectName"];
            var deploymentName = configuration["Azure:DeploymentName"];
            var speechKey = configuration["Azure:SpeechKey"];
            var speechRegion = configuration["Azure:SpeechRegion"];

            QuestionAnsweringClient client = new QuestionAnsweringClient(endpoint, credential);
            QuestionAnsweringProject project = new QuestionAnsweringProject(projectName, deploymentName);

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            AudioConfig audioConfig = null;

            // Check if microphone is available
            try
            {
                audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microphone not detected or not available. Falling back to text input.");
            }

            Console.WriteLine("Ask me anything about cats! Enter your question or press 'Enter' to speak. Type 'exit' to quit");

            while (true)
            {
                Console.Write("Q: ");
                string question = Console.ReadLine();

                // If the typed input is empty, fallback to voice input
                if (string.IsNullOrEmpty(question) && audioConfig != null)
                {
                    Console.WriteLine("Listening for voice input...");
                    question = await RecognizeSpeechAsync(speechConfig, audioConfig);

                    // Output the recognized speech as text
                    if (!string.IsNullOrEmpty(question))
                    {
                        Console.WriteLine($"Recognized Speech: {question}");
                    }
                }

                // Handle exit command
                if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    await SynthesizeSpeechAsync(speechConfig, "Goodbye!");
                    Console.WriteLine("Goodbye!");
                    break;
                }

                if (!string.IsNullOrEmpty(question))
                {
                    try
                    {
                        Response<AnswersResult> response = client.GetAnswers(question, project);
                        foreach (KnowledgeBaseAnswer answer in response.Value.Answers)
                        {
                            Console.WriteLine($"A: {answer.Answer}");

                            // Text to Speech
                            await SynthesizeSpeechAsync(speechConfig, answer.Answer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Request Error: {ex.Message}");
                    }
                }
            }
        }

        private static async Task<string> RecognizeSpeechAsync(SpeechConfig speechConfig, AudioConfig audioConfig)
        {
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var result = await recognizer.RecognizeOnceAsync();

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                // Directly check if the recognized speech is "exit"
                if (result.Text.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    return "exit";
                }

                return result.Text;
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                Console.WriteLine("No speech could be recognized.");
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = CancellationDetails.FromResult(result);
                Console.WriteLine($"Speech Recognition Canceled: {cancellation.Reason}");

                if (cancellation.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"Error Details: {cancellation.ErrorDetails}");
                }
            }

            return string.Empty;
        }

        private static async Task SynthesizeSpeechAsync(SpeechConfig speechConfig, string text)
        {
            using var synthesizer = new SpeechSynthesizer(speechConfig);
            await synthesizer.SpeakTextAsync(text);
        }
    }
}