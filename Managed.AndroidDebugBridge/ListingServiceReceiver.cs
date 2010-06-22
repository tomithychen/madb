﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Managed.Adb {
	public class ListingServiceReceiver : MultiLineReceiver {

		/// <summary>
		/// Create an ls receiver/parser.
		/// </summary>
		/// <param name="parent">The list of current children. To prevent collapse during update, reusing the same 
		/// FileEntry objects for files that were already there is paramount.</param>
		/// <param name="entries">the list of new children to be filled by the receiver.</param>
		/// <param name="links">the list of link path to compute post ls, to figure out if the link 
		/// pointed to a file or to a directory.</param>
		public ListingServiceReceiver ( FileEntry parent, List<FileEntry> entries, List<String> links ) {
			Parent = parent;
			Entries = entries;
			Links = links;
			CurrentChildren = Parent.Children.ToArray ( );
		}

		public List<FileEntry> Entries { get; private set; }
		public List<String> Links { get; private set; }
		public FileEntry[] CurrentChildren { get; private set; }
		public FileEntry Parent { get; private set; }

		public override void ProcessNewLines ( string[] lines ) {
			foreach ( String line in lines ) {
				// no need to handle empty lines.
				if ( line.Length == 0 ) {
					continue;
				}

				// run the line through the regexp
				Regex regex = new Regex ( FileListingService.LS_PATTERN, RegexOptions.Compiled );
				Match m = regex.Match ( line );
				if ( !m.Success ) {
					continue;
				}

				// get the name
				String name = m.Groups[7].Value;

				// if the parent is root, we only accept selected items
				if ( Parent.IsRoot ) {
					bool found = false;
					foreach ( String approved in FileListingService.RootLevelApprovedItems ) {
						if ( String.Compare ( approved, name, false ) == 0 ) {
							found = true;
							break;
						}
					}

					// if it's not in the approved list we skip this entry.
					if ( found == false ) {
						continue;
					}
				}

				// get the rest of the groups
				String permissions = m.Groups[1].Value;
				String owner = m.Groups[2].Value;
				String group = m.Groups[3].Value;
				long size = 0;
				long.TryParse ( m.Groups[4].Value, out size );
				String date = m.Groups[5].Value;
				String time = m.Groups[6].Value;
				String info = null;

				// and the type
				FileListingService.FileTypes objectType = FileListingService.FileTypes.Other;
				switch ( permissions[0] ) {
					case '-':
						objectType = FileListingService.FileTypes.File;
						break;
					case 'b':
						objectType = FileListingService.FileTypes.Block;
						break;
					case 'c':
						objectType = FileListingService.FileTypes.Character;
						break;
					case 'd':
						objectType = FileListingService.FileTypes.Directory;
						break;
					case 'l':
						objectType = FileListingService.FileTypes.Link;
						break;
					case 's':
						objectType = FileListingService.FileTypes.Socket;
						break;
					case 'p':
						objectType = FileListingService.FileTypes.FIFO;
						break;
				}


				// now check what we may be linking to
				if ( objectType == FileListingService.FileTypes.Link ) {
					String[] segments = name.Split ( new string[] { "\\s->\\s" }, StringSplitOptions.RemoveEmptyEntries ); //$NON-NLS-1$

					// we should have 2 segments
					if ( segments.Length == 2 ) {
						// update the entry name to not contain the link
						name = segments[0];

						// and the link name
						info = segments[1];

						// now get the path to the link
						String[] pathSegments = info.Split ( new String[] { FileListingService.FILE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries );
						if ( pathSegments.Length == 1 ) {
							// the link is to something in the same directory,
							// unless the link is ..
							if ( String.Compare ( "..", pathSegments[0], false ) == 0 ) { //$NON-NLS-1$
								// set the type and we're done.
								objectType = FileListingService.FileTypes.DirectoryLink;
							} else {
								// either we found the object already
								// or we'll find it later.
							}
						}
					}

					// add an arrow in front to specify it's a link.
					info = "-> " + info; //$NON-NLS-1$;
				}

				// get the entry, either from an existing one, or a new one
				FileEntry entry = GetExistingEntry ( name );
				if ( entry == null ) {
					entry = new FileEntry ( Parent, name, objectType, false /* isRoot */);
				}

				// add some misc info
				entry.Permissions = permissions;
				entry.Size = size;
				entry.Date = date;
				entry.Time = time;
				entry.Owner = owner;
				entry.Group = group;
				if ( objectType == FileListingService.FileTypes.Link ) {
					entry.Info = info;
				}

				Entries.Add ( entry );
			}
		}


		public override bool IsCancelled {
			get {
				return false;
			}
		}

		/// <summary>
		/// Queries for an already existing Entry per name
		/// </summary>
		/// <param name="name">the name of the entry</param>
		/// <returns>the existing FileEntry or null if no entry with a matching name exists.</returns>
		private FileEntry GetExistingEntry ( String name ) {
			for ( int i = 0; i < CurrentChildren.Length; i++ ) {
				FileEntry e = CurrentChildren[i];
				// since we're going to "erase" the one we use, we need to
				// check that the item is not null.
				if ( e != null ) {
					// compare per name, case-sensitive.
					if ( string.Compare ( name, e.Name, false ) == 0 ) {
						// erase from the list
						CurrentChildren[i] = null;
						// and return the object
						return e;
					}
				}
			}

			// couldn't find any matching object, return null
			return null;
		}


		public void FinishLinks ( ) {
			// this isnt done in the DDMS lib either... 
			// TODO: Handle links in the listing service
		}
	}
}