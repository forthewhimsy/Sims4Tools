/****************************************************************************
 *  Copyright (C) 2026 by forthewhimsy                                     *
 *                                                                         *
 *  This file is part of the Sims 4 Package Editor (s4pe)                  *
 *                                                                         *
 *  s4pe is free software: you can redistribute it and/or modify           *
 *  it under the terms of the GNU General Public License as published by   *
 *  the Free Software Foundation, either version 3 of the License, or      *
 *  (at your option) any later version.                                    *
 *                                                                         *
 *  s4pe is distributed in the hope that it will be useful,                *
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of         *
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the          *
 *  GNU General Public License for more details.                           *
 *                                                                         *
 *  You should have received a copy of the GNU General Public License      *
 *  along with s4pe.  If not, see <http://www.gnu.org/licenses/>.          *
 ***************************************************************************/
using System;
using System.Drawing;
using System.Windows.Forms;
using s4pi.Interfaces;

namespace S4PIDemoFE
{
    /// <summary>
    /// Asked once when an incoming clip and the clip it would replace both carry
    /// ClipEvents, so that a batch import does not put up one prompt per clip.
    /// </summary>
    internal class ClipEventConflictDialog : Form
    {
        private readonly RadioButton keepExisting;
        private readonly RadioButton keepIncoming;
        private readonly RadioButton merge;
        private readonly CheckBox applyToAll;

        /// <summary>What to do with the events of the clip being replaced.</summary>
        public EventPreservation Choice
        {
            get
            {
                if (this.keepIncoming.Checked)
                {
                    return EventPreservation.KeepIncoming;
                }
                return this.merge.Checked ? EventPreservation.Merge : EventPreservation.KeepExisting;
            }
        }

        /// <summary>Whether to reuse this answer for the rest of the current import.</summary>
        public bool ApplyToAll
        {
            get { return this.applyToAll.Checked; }
        }

        public ClipEventConflictDialog(IResourceKey rk, int existingEvents, int incomingEvents)
        {
            this.Text = "Clip events";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(470, 244);

            var summary = new Label
                          {
                              AutoSize = false,
                              Location = new Point(12, 12),
                              Size = new Size(446, 48),
                              Text = string.Format(
                                  "The clip already in this package has {0} clip event{1}."
                                  + "\r\nThe clip you are importing brings {2} of its own."
                                  + "\r\n{3}",
                                  existingEvents,
                                  existingEvents == 1 ? "" : "s",
                                  incomingEvents,
                                  rk)
                          };

            this.keepExisting = new RadioButton
                                {
                                    AutoSize = false,
                                    Checked = true,
                                    Location = new Point(16, 68),
                                    Size = new Size(442, 20),
                                    Text = "&Keep the events already in the package (discard the incoming ones)"
                                };
            this.keepIncoming = new RadioButton
                                {
                                    AutoSize = false,
                                    Location = new Point(16, 94),
                                    Size = new Size(442, 20),
                                    Text = "&Use the incoming clip's events (discard the ones already in the package)"
                                };
            this.merge = new RadioButton
                         {
                             AutoSize = false,
                             Location = new Point(16, 120),
                             Size = new Size(442, 20),
                             Text = "&Merge - keep both sets, dropping only exact duplicates"
                         };
            this.applyToAll = new CheckBox
                              {
                                  AutoSize = false,
                                  Checked = true,
                                  Location = new Point(16, 154),
                                  Size = new Size(442, 20),
                                  Text = "&Apply this to every other clip in this import"
                              };

            var ok = new Button
                     {
                         Text = "OK",
                         DialogResult = DialogResult.OK,
                         Location = new Point(292, 200),
                         Size = new Size(80, 26)
                     };
            // Cancel is handled by the caller, which takes the conservative choice for this
            // resource alone rather than treating a non-answer as one to apply to the batch.
            var cancel = new Button
                         {
                             Text = "Cancel",
                             DialogResult = DialogResult.Cancel,
                             Location = new Point(378, 200),
                             Size = new Size(80, 26)
                         };
            this.Controls.AddRange(new Control[]
                                   {
                                       summary, this.keepExisting, this.keepIncoming, this.merge,
                                       this.applyToAll, ok, cancel
                                   });
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }
    }
}
