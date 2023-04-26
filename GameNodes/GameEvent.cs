using System;
using JetBrains.Annotations;

namespace GameNodes
{
    [MeansImplicitUse(ImplicitUseKindFlags.Access)]
    [AttributeUsage(AttributeTargets.Method)]
    public abstract class GameEvent : Attribute
    {
    }
}