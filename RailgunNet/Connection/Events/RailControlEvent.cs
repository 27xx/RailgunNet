﻿/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Railgun
{
  public class RailControlEvent : RailEvent<RailControlEvent>
  {
    protected internal override int EventType
    {
      get { return RailEventTypes.TYPE_CONTROL; }
    }

    internal int EntityId { get; set; }
    internal bool Granted { get; set; }

    internal void SetData(
      int entityId,
      bool granted)
    {
      this.EntityId = entityId;
      this.Granted = granted;
    }

    protected override void ResetData()
    {
      this.EntityId = RailEntity.INVALID_ID;
      this.Granted = false;
    }

    protected internal override void SetDataFrom(RailControlEvent other)
    {
      this.EntityId = other.EntityId;
      this.Granted = other.Granted;
    }

    protected override void EncodeData(BitBuffer buffer)
    {
      // Write: [EntityId]
      buffer.Push(StandardEncoders.EntityId, this.EntityId);

      // Write: [Granted]
      buffer.Push(StandardEncoders.Bool, this.Granted);
    }

    protected override void DecodeData(BitBuffer buffer)
    {
      // Write: [Granted]
      this.Granted = buffer.Pop(StandardEncoders.Bool);

      // Write: [EntityId]
      this.EntityId = buffer.Pop(StandardEncoders.EntityId);
    }
  }
}