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

namespace s4pi.Interfaces
{
    /// <summary>
    /// What to do with the events of a resource that is about to be replaced.
    /// </summary>
    public enum EventPreservation
    {
        /// <summary>Carry the events already in the package over to the incoming resource.</summary>
        KeepExisting,

        /// <summary>Leave the incoming resource alone; its own events win.</summary>
        KeepIncoming,

        /// <summary>Keep the incoming events and append any existing ones they do not already contain.</summary>
        Merge,
    }

    /// <summary>
    /// Implemented by resource wrappers that carry hand authored event data which is
    /// expensive to recreate and should be able to survive the resource being replaced.
    /// </summary>
    /// <remarks>
    /// Replacing a resource is otherwise a byte level delete-and-add, so anything the
    /// author entered by hand in the editor is lost the moment a freshly exported
    /// version of the same resource is imported over it. A wrapper that implements
    /// this interface lets the shell offer to carry that data across instead.
    /// <para>Both methods take and return whole serialized resources so that the shell
    /// never has to know the layout of the format, and so that an implementation is
    /// free to transplant the events without re-serializing anything else.</para>
    /// </remarks>
    public interface IEventPreservingResource
    {
        /// <summary>
        /// The number of preservable events in <paramref name="resource"/>, or -1 if it
        /// is not a resource this wrapper recognises.
        /// </summary>
        /// <param name="resource">A whole serialized resource of this wrapper's type.</param>
        int CountPreservableEvents(byte[] resource);

        /// <summary>
        /// Returns <paramref name="incoming"/> with the events of <paramref name="existing"/>
        /// applied to it according to <paramref name="mode"/>.
        /// </summary>
        /// <param name="warning">
        /// Set to a message worth showing the user about the result, or null when there is
        /// nothing to say. The wrapper raises this itself because only it knows what would
        /// make a result questionable.
        /// </param>
        /// <returns>
        /// The resulting resource, or <paramref name="incoming"/> unchanged if either
        /// resource could not be read.
        /// </returns>
        byte[] PreserveEvents(byte[] incoming, byte[] existing, EventPreservation mode, out string warning);
    }
}
