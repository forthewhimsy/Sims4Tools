/****************************************************************************
 *  Copyright (C) 2026 by forthewhimsy                                     *
 *                                                                         *
 *  This file is part of the Sims 4 Package Interface (s4pi)               *
 *                                                                         *
 *  s4pi is free software: you can redistribute it and/or modify           *
 *  it under the terms of the GNU General Public License as published by   *
 *  the Free Software Foundation, either version 3 of the License, or      *
 *  (at your option) any later version.                                    *
 *                                                                         *
 *  s4pi is distributed in the hope that it will be useful,                *
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of         *
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the          *
 *  GNU General Public License for more details.                           *
 *                                                                         *
 *  You should have received a copy of the GNU General Public License      *
 *  along with s4pi.  If not, see <http://www.gnu.org/licenses/>.          *
 ***************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;

namespace s4pi.Animation
{
    /// <summary>
    /// Byte level access to the ClipEvent block of a serialized CLIP resource.
    /// </summary>
    /// <remarks>
    /// Transplanting events between two clips is deliberately done on the raw bytes
    /// rather than by re-serializing a parsed <see cref="ClipResource"/>. The event
    /// block is wholly self-contained - every event record carries its own length,
    /// and event payloads hold no offsets into the rest of the resource - so it can
    /// be cut out and pasted in without disturbing a single byte of the S3CLIP codec
    /// data that follows it. Going through <see cref="ClipResource.UnParse"/> instead
    /// would rewrite the entire resource, and that is not byte-faithful for every
    /// clip in the wild (fixed-length strings lose trailing spaces, and the S3CLIP
    /// writer lays its channel data out differently from the game's own exporter).
    /// </remarks>
    public static class ClipEventBlock
    {
        /// <summary>Position and size of the ClipEvent block within a serialized clip.</summary>
        public struct Location
        {
            /// <summary>Offset of the event count field that opens the block.</summary>
            public int Start;

            /// <summary>Offset of the first byte after the last event record.</summary>
            public int End;

            /// <summary>Number of event records in the block.</summary>
            public int Count;
        }

        /// <summary>
        /// Walks the fixed portion of a serialized clip to find its ClipEvent block.
        /// Returns false - rather than throwing - for anything that does not parse
        /// cleanly, so that callers can fall back to leaving the resource alone.
        /// </summary>
        public static bool TryLocate(byte[] clip, out Location location)
        {
            location = new Location();
            if (clip == null)
            {
                return false;
            }

            int p = 0;
            uint version;
            if (!ReadUInt32(clip, ref p, out version))
            {
                return false;
            }

            // flags, duration, Quaternion (4 floats), Vector3 (3 floats)
            if (!Skip(clip, ref p, 4 + 4 + 16 + 12))
            {
                return false;
            }
            if (version >= 5 && !Skip(clip, ref p, 4))            // referenceNamespaceHash
            {
                return false;
            }
            if (version >= 10 && !Skip(clip, ref p, 8))           // surfaceNamespaceHash, surfaceJointNameHash
            {
                return false;
            }
            if (version >= 11 && !Skip(clip, ref p, 4))           // surfacechildNamespaceHash
            {
                return false;
            }
            if (version >= 7 && !SkipString32(clip, ref p))       // clip_name
            {
                return false;
            }
            if (!SkipString32(clip, ref p))                       // rigNameSpace
            {
                return false;
            }
            if (version >= 4 && !SkipList(clip, ref p, SkipString32))
            {
                return false;
            }
            if (!SkipList(clip, ref p, SkipSlotAssignment))
            {
                return false;
            }

            int start = p;
            uint count;
            if (!ReadUInt32(clip, ref p, out count))
            {
                return false;
            }
            for (uint i = 0; i < count; i++)
            {
                uint size;
                if (!Skip(clip, ref p, 4) || !ReadUInt32(clip, ref p, out size) || !Skip(clip, ref p, (long)size))
                {
                    return false;
                }
            }
            int end = p;

            // The event block is immediately followed by the codec data length, and
            // that length covers every remaining byte. If that does not add up we
            // mis-walked the header somewhere and must not touch this resource.
            uint codecLength;
            if (!ReadUInt32(clip, ref p, out codecLength) || (long)end + 4 + codecLength != clip.Length)
            {
                return false;
            }

            location.Start = start;
            location.End = end;
            location.Count = (int)count;
            return true;
        }

        /// <summary>
        /// Number of ClipEvents in a serialized clip, or -1 if it could not be walked.
        /// </summary>
        public static int CountEvents(byte[] clip)
        {
            Location location;
            return TryLocate(clip, out location) ? location.Count : -1;
        }

        /// <summary>
        /// Splits the event block of a serialized clip into one byte array per event
        /// record, each record being its type id, its length and its payload.
        /// </summary>
        public static List<byte[]> SplitEvents(byte[] clip, Location location)
        {
            var events = new List<byte[]>(location.Count);
            int p = location.Start + 4;
            for (int i = 0; i < location.Count; i++)
            {
                int recordStart = p;
                uint size = ToUInt32(clip, p + 4);
                p += 8 + (int)size;
                var record = new byte[p - recordStart];
                Buffer.BlockCopy(clip, recordStart, record, 0, record.Length);
                events.Add(record);
            }
            return events;
        }

        /// <summary>
        /// Returns <paramref name="clip"/> with its event block replaced by
        /// <paramref name="events"/>. Every other byte is copied through untouched.
        /// </summary>
        public static byte[] ReplaceEvents(byte[] clip, Location location, IList<byte[]> events)
        {
            int payload = 0;
            for (int i = 0; i < events.Count; i++)
            {
                payload += events[i].Length;
            }

            var result = new byte[location.Start + 4 + payload + (clip.Length - location.End)];
            Buffer.BlockCopy(clip, 0, result, 0, location.Start);

            int p = location.Start;
            WriteUInt32(result, p, (uint)events.Count);
            p += 4;
            for (int i = 0; i < events.Count; i++)
            {
                Buffer.BlockCopy(events[i], 0, result, p, events[i].Length);
                p += events[i].Length;
            }
            Buffer.BlockCopy(clip, location.End, result, p, clip.Length - location.End);
            return result;
        }

        /// <summary>
        /// The duration of a serialized clip in seconds, or -1 if it is too short to hold one.
        /// </summary>
        public static float ReadDuration(byte[] clip)
        {
            return clip == null || clip.Length < 12 ? -1f : ToSingle(clip, 8);
        }

        /// <summary>
        /// The timecode of one event record as returned by <see cref="SplitEvents"/>.
        /// </summary>
        /// <remarks>
        /// A record is its type id and length followed by the event's own unknown1,
        /// unknown2 and timecode, so the timecode sits 16 bytes in whatever the event type.
        /// </remarks>
        public static float ReadTimecode(byte[] record)
        {
            return record == null || record.Length < 20 ? 0f : ToSingle(record, 16);
        }

        /// <summary>
        /// Orders event records by timecode, keeping records that share a timecode in the
        /// order they were given.
        /// </summary>
        public static void SortByTimecode(IList<byte[]> events)
        {
            // Decorate with the original position so equal timecodes keep their order;
            // List.Sort on its own is not stable.
            var ordered = new List<KeyValuePair<int, byte[]>>(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                ordered.Add(new KeyValuePair<int, byte[]>(i, events[i]));
            }
            ordered.Sort(delegate(KeyValuePair<int, byte[]> a, KeyValuePair<int, byte[]> b)
                         {
                             int byTime = ReadTimecode(a.Value).CompareTo(ReadTimecode(b.Value));
                             return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
                         });
            for (int i = 0; i < events.Count; i++)
            {
                events[i] = ordered[i].Value;
            }
        }

        /// <summary>
        /// How many of <paramref name="events"/> fall after <paramref name="duration"/>.
        /// </summary>
        public static int CountPastEnd(IList<byte[]> events, float duration)
        {
            if (duration < 0f)
            {
                return 0;
            }
            int past = 0;
            for (int i = 0; i < events.Count; i++)
            {
                // A small tolerance so an event sitting exactly on the last frame,
                // which is normal, is not reported.
                if (ReadTimecode(events[i]) > duration + 0.0001f)
                {
                    past++;
                }
            }
            return past;
        }

        #region Primitives

        private delegate bool Skipper(byte[] data, ref int p);

        private static uint ToUInt32(byte[] data, int p)
        {
            return (uint)data[p] | ((uint)data[p + 1] << 8) | ((uint)data[p + 2] << 16) | ((uint)data[p + 3] << 24);
        }

        private static float ToSingle(byte[] data, int p)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(ToUInt32(data, p)), 0);
        }

        private static void WriteUInt32(byte[] data, int p, uint value)
        {
            data[p] = (byte)value;
            data[p + 1] = (byte)(value >> 8);
            data[p + 2] = (byte)(value >> 16);
            data[p + 3] = (byte)(value >> 24);
        }

        private static bool Skip(byte[] data, ref int p, long count)
        {
            if (count < 0 || p + count > data.Length)
            {
                return false;
            }
            p += (int)count;
            return true;
        }

        private static bool ReadUInt32(byte[] data, ref int p, out uint value)
        {
            value = 0;
            if (p + 4 > data.Length)
            {
                return false;
            }
            value = ToUInt32(data, p);
            p += 4;
            return true;
        }

        private static bool SkipString32(byte[] data, ref int p)
        {
            uint length;
            return ReadUInt32(data, ref p, out length) && Skip(data, ref p, length);
        }

        private static bool SkipSlotAssignment(byte[] data, ref int p)
        {
            // chainId (ushort), slotID (ushort), targetObjectNamespace, targetJointName
            return Skip(data, ref p, 4) && SkipString32(data, ref p) && SkipString32(data, ref p);
        }

        private static bool SkipList(byte[] data, ref int p, Skipper skipper)
        {
            uint count;
            if (!ReadUInt32(data, ref p, out count))
            {
                return false;
            }
            for (uint i = 0; i < count; i++)
            {
                if (!skipper(data, ref p))
                {
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
