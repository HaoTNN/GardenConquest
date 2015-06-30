using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using Interfaces = Sandbox.ModAPI.Interfaces;
using InGame = Sandbox.ModAPI.Ingame;

using GardenConquest.Blocks;
using GardenConquest.Records;
using GardenConquest.Core;

namespace GardenConquest.Messaging {
	/// <summary>
	/// Client side message hooks.  Processed messages coming from the server.
	/// </summary>
	public class ResponseProcessor {
		private static Logger s_Logger = null;

        private Dictionary<int, List<FactionFleet.GridData>> m_SupportedGrids;
        private Dictionary<int, List<FactionFleet.GridData>> m_UnsupportedGrids;

		private ConquestSettings.SETTINGS m_ServerSettings;
		private bool m_Registered = false;

		public ConquestSettings.SETTINGS ServerSettings {
			get {return m_ServerSettings; }
		}

		public ResponseProcessor(bool register = true) {
			log("Started", "ctor");
			if (register && MyAPIGateway.Multiplayer != null) {
				log("Registering for messages", "ctor");
				m_Registered = true;
				MyAPIGateway.Multiplayer.RegisterMessageHandler(Constants.GCMessageId, incomming);
			}
            m_SupportedGrids = new Dictionary<int, List<FactionFleet.GridData>>();
            m_UnsupportedGrids = new Dictionary<int, List<FactionFleet.GridData>>();
		}

		public void unload() {
			if (m_Registered && MyAPIGateway.Multiplayer != null)
				MyAPIGateway.Multiplayer.UnregisterMessageHandler(Constants.GCMessageId, incomming);
		}

		#region Make Requests

		public void send(BaseRequest msg) {
			if (msg == null)
				return;

			byte[] buffer = msg.serialize();
			MyAPIGateway.Multiplayer.SendMessageToServer(Constants.GCMessageId, buffer);
			log("Sent packet of " + buffer.Length + " bytes", "send");
		}

		public bool requestSettings() {
			log("Sending Settings request", "requestSettings");
			try {
				SettingsRequest req = new SettingsRequest();
				req.ReturnAddress = MyAPIGateway.Session.Player.PlayerID;
				send(req);
				return true;
			}
			catch (Exception e) {
				log("Exception occured: " + e, "requestSettings", Logger.severity.ERROR);
				return false;
			}
		}

		public bool requestFleet(String hullClassString = "") {
			log("Sending Fleet request", "requestFleet");
			try {
				FleetRequest req = new FleetRequest();
				req.ReturnAddress = MyAPIGateway.Session.Player.PlayerID;
				send(req);
				return true;
			}
			catch (Exception e) {
				log("Exception occured: " + e, "requestFleet");
				return false;
			}
		}

		public bool requestViolations(String hullClassString = "") {
			log("Sending Violations request", "requestViolations");
			try {
				ViolationsRequest req = new ViolationsRequest();
				req.ReturnAddress = MyAPIGateway.Session.Player.PlayerID;
				send(req);
				return true;
			}
			catch (Exception e) {
				log("Exception occured: " + e, "requestViolations");
				return false;
			}
		}

		#endregion
		#region Process Responses

		public void incomming(byte[] buffer) {
			log("Got message of size " + buffer.Length, "incomming");

			try {
				// Deserialize the message
				BaseResponse msg = BaseResponse.messageFromBytes(buffer);

				// Is this message even intended for us?
				if (msg.DestType == BaseResponse.DEST_TYPE.FACTION) {
					IMyFaction fac = MyAPIGateway.Session.Factions.TryGetPlayerFaction(
						MyAPIGateway.Session.Player.PlayerID);
					if (fac == null || !msg.Destination.Contains(fac.FactionId)) {
						return; // Message not meant for us
					}
				} else if (msg.DestType == BaseResponse.DEST_TYPE.PLAYER) {
					long localUserId = (long)MyAPIGateway.Session.Player.PlayerID;
					if (!msg.Destination.Contains(localUserId)) {
						return; // Message not meant for us
					}
				}

				switch (msg.MsgType) {
					case BaseResponse.TYPE.NOTIFICATION:
						processNotificationResponse(msg as NotificationResponse);
						break;
					case BaseResponse.TYPE.DIALOG:
						processDialogResponse(msg as DialogResponse);
						break;
					case BaseResponse.TYPE.SETTINGS:
						processSettingsResponse(msg as SettingsResponse);
						break;
                    case BaseResponse.TYPE.FLEET:
                        processFleetResponse(msg as FleetResponse);
                        break;
				}
			} catch (Exception e) {
				log("Exception occured: " + e, "incomming", Logger.severity.ERROR);
			}
		}

		private void processNotificationResponse(NotificationResponse noti) {
			log("Hit", "processNotificationResponse");
			MyAPIGateway.Utilities.ShowNotification(noti.NotificationText, noti.Time, noti.Font);
		}

		private void processDialogResponse(DialogResponse resp) {
			log("Hit", "processDialogResponse");
			Utility.showDialog(resp.Title, resp.Body, "Close");
		}

		private void processSettingsResponse(SettingsResponse resp) {
			log("Loading settings from server", "processSettingsResponse");
			m_ServerSettings = resp.Settings;

			log("Adding CP GPS", "processSettingsResponse");
			foreach (Records.ControlPoint cp in ServerSettings.ControlPoints) {
				IMyGps gps = MyAPIGateway.Session.GPS.Create(
					cp.Name, "GardenConquest Control Point",
					new VRageMath.Vector3D(cp.Position.X, cp.Position.Y, cp.Position.Z),
					true, true);
				MyAPIGateway.Session.GPS.AddLocalGps(gps);
			}
		}

        private void processFleetResponse(FleetResponse resp) {
            log("Loading fleet data from server", "processFleetResponse", Logger.severity.WARNING);
            List<FactionFleet.GridData> gridData = resp.FleetData;

            // Clear our current data to get fresh data from server
            m_SupportedGrids.Clear();
            m_UnsupportedGrids.Clear();

            // Building the title
            string fleetInfoTitle = "";
            switch (resp.OwnerType) {
                case GridOwner.OWNER_TYPE.FACTION:
                    fleetInfoTitle = "Your Faction's Fleet:";
                    break;
                case GridOwner.OWNER_TYPE.PLAYER:
                    fleetInfoTitle = "Your Fleet:";
                    break;
            }
            string fleetInfoBody = "";

            // Saving data from server to client
            for (int i = 0; i < gridData.Count; ++i) {
                if (gridData[i].supported) {
                    if (!m_SupportedGrids.ContainsKey((int)gridData[i].shipClass)) {
                        m_SupportedGrids[(int)gridData[i].shipClass] = new List<FactionFleet.GridData>();
                    }
                    m_SupportedGrids[(int)gridData[i].shipClass].Add(gridData[i]);
                }
                else {
                    if (!m_UnsupportedGrids.ContainsKey((int)gridData[i].shipClass)) {
                        m_UnsupportedGrids[(int)gridData[i].shipClass] = new List<FactionFleet.GridData>();
                    }
                    m_UnsupportedGrids[(int)gridData[i].shipClass].Add(gridData[i]);
                }
            }

            // Go through our data to get the fleet information
            // Consider getting the fleet information in a new function for cleaner code?
            foreach (KeyValuePair<int, List<FactionFleet.GridData>> entry in m_SupportedGrids) {
                fleetInfoBody += Enum.GetName(typeof(HullClass.CLASS), entry.Key) + ": " + entry.Value.Count;
                if (resp.OwnerType == GridOwner.OWNER_TYPE.FACTION) {
                    fleetInfoBody += " / " + ServerSettings.HullRules[entry.Key].MaxPerFaction + "\n";
                }
                else if (resp.OwnerType == GridOwner.OWNER_TYPE.PLAYER) {
                    fleetInfoBody += " / " + ServerSettings.HullRules[entry.Key].MaxPerSoloPlayer + "\n";
                }
                else {
                    fleetInfoBody += " / 0" + "\n";
                }
                for (int i = 0; i < entry.Value.Count; ++i) {
                    fleetInfoBody += "  " + i + ". " + entry.Value[i].shipName + " - " + entry.Value[i].blockCount + " blocks\n";
                }
            }
            fleetInfoBody += "\n  Unsupported:\n";
            foreach (KeyValuePair<int, List<FactionFleet.GridData>> entry in m_UnsupportedGrids) {
                for (int i = 0; i < entry.Value.Count; ++i) {
                    fleetInfoBody += "     " + i + ". " + entry.Value[i].shipName + " - " + entry.Value[i].blockCount + " blocks\n";
                }
            }
            fleetInfoBody += "\n";

            // Displaying the fleet information
            Utility.showDialog(fleetInfoTitle, fleetInfoBody, "Close");
        }

		#endregion

		private static void log(String message, String method = null, Logger.severity level = Logger.severity.DEBUG) {
			if (s_Logger == null)
				s_Logger = new Logger("Conquest Core", "ResponseProcessor");

			s_Logger.log(level, method, message);
		}

	}
}
