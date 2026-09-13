using System.Threading.Tasks;

namespace Simulator_best
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            using var simulator = new UDPSimulator();
            simulator.Initialize();
            await simulator.EncodingRawMessage();
        }
    }
}
