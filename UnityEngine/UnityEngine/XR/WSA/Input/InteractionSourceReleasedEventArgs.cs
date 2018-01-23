﻿// Decompiled with JetBrains decompiler
// Type: UnityEngine.XR.WSA.Input.InteractionSourceReleasedEventArgs
// Assembly: UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: D290425A-E4B3-4E49-A420-29F09BB3F974
// Assembly location: C:\Program Files\Unity 5\Editor\Data\Managed\UnityEngine.dll

using System.Runtime.InteropServices;

namespace UnityEngine.XR.WSA.Input
{
  /// <summary>
  ///   <para>Contains fields that are relevent when an interaction source exits the pressed state for one of its buttons.</para>
  /// </summary>
  [StructLayout(LayoutKind.Sequential, Size = 1)]
  public struct InteractionSourceReleasedEventArgs
  {
    public InteractionSourceReleasedEventArgs(InteractionSourceState state, InteractionSourcePressType pressType)
    {
      this = new InteractionSourceReleasedEventArgs();
      this.state = state;
      this.pressType = pressType;
    }

    /// <summary>
    ///   <para>The current state of the reported interaction source that just had one of its buttons exit the pressed state.</para>
    /// </summary>
    public InteractionSourceState state { get; private set; }

    /// <summary>
    ///   <para>Denotes the type of button that was just released.</para>
    /// </summary>
    public InteractionSourcePressType pressType { get; private set; }
  }
}