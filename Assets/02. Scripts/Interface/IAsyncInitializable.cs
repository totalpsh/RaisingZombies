using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public interface IAsyncInitializable
{
    UniTask InitializeAsync(CancellationToken cancellationToken);
}
