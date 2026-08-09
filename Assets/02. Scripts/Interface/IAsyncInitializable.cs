using System.Threading;
using System.Threading.Tasks;

public interface IAsyncInitializable
{
    Task InitializeAsync();
}
