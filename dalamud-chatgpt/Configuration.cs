using Dalamud.Configuration;
using Dalamud.Plugin;

namespace xivgpt
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }
        public bool RemoveLineBreaks { get; set; } = false;
        public bool ShowAdditionalInfo { get; set; }
        public string ApiKey { get; set; } = "";
        public int MaxTokens { get; set; }
        public const string Endpoint = "https://api.openai.com/v1/completions";
        public const string Model = "text-davinci-003";


        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pInterface)
        {
            pluginInterface = pInterface;
        }

        public void Save()
        {
            pluginInterface.SavePluginConfig(this);
        }
    }
}
