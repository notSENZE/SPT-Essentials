using EFT;
using EFT.Interactive;
using UnityEngine;

namespace SPTEssentials.Client.Breacher;

internal sealed class PlacedBreachingCharge : MonoBehaviour
{
    internal Door Door;
    internal Player PlacedBy;
    internal float DetonationTime;
    internal float NextBeepTime;
    internal AudioSource AudioSource;
}
