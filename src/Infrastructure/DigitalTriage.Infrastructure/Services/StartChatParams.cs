using Mscc.GenerativeAI;

namespace DigitalTriage.Infrastructure.Services
{
    internal class StartChatParams : List<ContentResponse>
    {
        public List<Content> History { get; set; }
    }
}