﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CommonTools;

namespace Railgun
{
  interface IRailPacket
  {
    void Encode(BitBuffer buffer);
  }

  internal abstract class RailPacket : IRailPacket
  {
    // String hashes (md5):
    private const int KEY_RESERVE_1 = 0x137CE785;
    private const int KEY_RESERVE_2 = 0x4E9C95DA;
    private const int KEY_ROLLBACK = 0x652FC8E6;

    /// <summary>
    /// Minimum number of reliable events to send.
    /// </summary>
    internal const int MIN_EVENT_SEND = 3;

    /// <summary>
    /// The latest tick from the sender.
    /// </summary>
    internal Tick SenderTick { get { return this.senderTick; } }

    /// <summary>
    /// The last tick the sender received.
    /// </summary>
    internal Tick AckTick { get { return this.ackTick; } }

    /// <summary>
    /// The last global reliable event id the sender received.
    /// </summary>
    internal EventId AckEventId { get { return this.ackEventId; } }

    /// <summary>
    /// Global reliable events from the sender, in order.
    /// </summary>
    internal IEnumerable<RailEvent> Events { get { return this.events; } }

    private Tick senderTick;
    private Tick ackTick;
    private EventId ackEventId;

    private readonly List<RailEvent> pendingEvents;
    private readonly List<RailEvent> events;

    private int eventsWritten;

    public RailPacket()
    {
      this.senderTick = Tick.INVALID;
      this.ackTick = Tick.INVALID;
      this.ackEventId = EventId.INVALID;

      this.pendingEvents = new List<RailEvent>();
      this.events = new List<RailEvent>();

      this.eventsWritten = 0;
    }

    internal void Initialize(
      Tick senderTick,
      Tick ackTick,
      EventId ackEventId,
      IEnumerable<RailEvent> events)
    {
      this.senderTick = senderTick;
      this.ackTick = ackTick;
      this.ackEventId = ackEventId;
      this.pendingEvents.AddRange(events);
    }

    protected virtual void Reset()
    {
      this.senderTick = Tick.INVALID;
      this.ackTick = Tick.INVALID;
      this.ackEventId = EventId.INVALID;

      this.pendingEvents.Clear();
      this.events.Clear();

      this.eventsWritten = 0;
    }

    #region Encoding/Decoding
    /// <summary>
    /// After writing the header we write the packet data in three passes.
    /// The first pass is a fill of events up to a percentage of the packet.
    /// The second pass is the payload value, which will try to fill the
    /// remaining packet space. If more space is available, we will try
    /// to fill it with any remaining events, up to the maximum packet size.
    /// </summary>
    public void Encode(BitBuffer buffer)
    {
      CommonDebug.Assert(buffer.IsAvailable(RailPacket.KEY_RESERVE_1));
      CommonDebug.Assert(buffer.IsAvailable(RailPacket.KEY_RESERVE_2));

      int firstPack = RailConfig.MESSAGE_FIRST_PACK;
      int secondPack = RailConfig.MESSAGE_MAX_SIZE;
      this.eventsWritten = 0;

      // Write: [Header]
      this.EncodeHeader(buffer);

      // Reserve: [Event Count] (for first pass)
      buffer.Reserve(RailPacket.KEY_RESERVE_1, RailEncoders.EventCount);

      // Reserve: [Event Count] (for third pass)
      buffer.Reserve(RailPacket.KEY_RESERVE_2, RailEncoders.EventCount);

      // Write: [Events] (first pass)
      this.PartialEncodeEvents(buffer, RailPacket.KEY_RESERVE_1, firstPack);

      // Write: [Payload] (second pass)
      this.EncodePayload(buffer);

      // Write: [Events] (third pass)
      this.PartialEncodeEvents(buffer, RailPacket.KEY_RESERVE_2, secondPack);
    }

    internal void Decode(BitBuffer buffer)
    {
      // Write: [Header]
      this.DecodeHeader(buffer);

      // Read: [Event Count] (for first pass)
      int firstCount = buffer.Read(RailEncoders.EventCount);

      // Read: [Event Count] (for third pass)
      int secondCount = buffer.Read(RailEncoders.EventCount);

      // Read: [Events] (first pass)
      this.PartialDecodeEvents(buffer, firstCount);

      // Write: [Payload] (second pass)
      this.DecodePayload(buffer);

      // Read: [Events] (third pass)
      this.PartialDecodeEvents(buffer, secondCount);
    }

    protected abstract void EncodePayload(BitBuffer buffer);
    protected abstract void DecodePayload(BitBuffer buffer);

    #region Header
    private void EncodeHeader(BitBuffer buffer)
    {
      // Write: [LocalTick]
      buffer.Write(RailEncoders.Tick, this.senderTick);

      // Write: [AckTick]
      buffer.Write(RailEncoders.Tick, this.ackTick);

      // Write: [AckEventId]
      buffer.Write(RailEncoders.EventId, this.ackEventId);
    }

    private void DecodeHeader(BitBuffer buffer)
    {
      // Read: [LocalTick]
      this.senderTick = buffer.Read(RailEncoders.Tick);

      // Read: [AckedTick]
      this.ackTick = buffer.Read(RailEncoders.Tick);

      // Read: [AckEventId]
      this.ackEventId = buffer.Read(RailEncoders.EventId);
    }

    #endregion

    #region Events
    /// <summary>
    /// Writes as many events as possible up to maxSize and returns the number
    /// of events written in the batch. Also increments the total counter.
    /// </summary>
    private void PartialEncodeEvents(
      BitBuffer buffer, 
      int keyReserve, 
      int maxSize)
    {
      // Count slot is already reserved prior to calling
      
      IEnumerable<RailEvent> packed =
        buffer.PackToSize<RailEvent>(
          keyReserve,
          RailPacket.KEY_ROLLBACK,
          RailEncoders.EventCount,
          this.GetWritableEvents(),
          maxSize,
          this.WriteEvent);

      foreach (RailEvent evnt in packed)
        this.eventsWritten++;
    }

    private void PartialDecodeEvents(BitBuffer buffer, int count)
    {
      // Read: [Events]
      for (int i = 0; i < count; i++)
        this.events.Add(RailEvent.Decode(buffer));
    }

    private IEnumerable<RailEvent> GetWritableEvents()
    {
      while (this.eventsWritten < this.pendingEvents.Count)
        yield return this.pendingEvents[this.eventsWritten];
    }

    private void WriteEvent(BitBuffer buffer, RailEvent evnt)
    {
      // Write: [Event]
      evnt.Encode(buffer);
    }
    #endregion
    #endregion
  }
}