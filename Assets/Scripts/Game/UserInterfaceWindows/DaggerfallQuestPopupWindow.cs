// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Hazelnut
// Contributors:
//
// Notes:
//

using UnityEngine;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    /// <summary>
    /// Abstract class for popup windows that can offer quests, including from summoned Daedra.
    /// </summary>
    public abstract class DaggerfallQuestPopupWindow : DaggerfallPopupWindow, IMacroContextProvider
    {
        protected const int NotEnoughGoldId = 454;
        protected const int SummonNotToday = 480;
        protected const int SummonAreYouSure = 481;
        protected const int SummonBefore = 482;
        protected const int SummonFailed = 483;

        public struct DaedraData
        {
            public readonly int factionId;
            public readonly int dayOfYear;
            public readonly string quest;
            public readonly string vidFile;

            public DaedraData(int factionId, string quest, int dayOfYear, string vidFile)
            {
                this.factionId = factionId;
                this.quest = quest;
                this.dayOfYear = dayOfYear;
                this.vidFile = vidFile;
            }
        }

        public static DaedraData[] daedraData = new DaedraData[] {
            new DaedraData((int) FactionFile.FactionIDs.Hircine, "X0C00Y00", 155, "HIRCINE.FLC"),  // Restrict to only glenmoril witches?
            new DaedraData((int) FactionFile.FactionIDs.Clavicus_Vile, "V0C00Y00", 1, "CLAVICUS.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Mehrunes_Dagon, "Y0C00Y00", 320, "MEHRUNES.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Molag_Bal, "20C00Y00", 350, "MOLAGBAL.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Sanguine, "70C00Y00", 46, "SANGUINE.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Peryite, "50C00Y00", 99, "PERYITE.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Malacath, "80C00Y00", 278, "MALACATH.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Hermaeus_Mora, "W0C00Y00", 65, "HERMAEUS.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Sheogorath, "60C00Y00", 32, "SHEOGRTH.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Boethiah, "U0C00Y00", 302, "BOETHIAH.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Namira, "30C00Y00", 129, "NAMIRA.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Meridia, "10C00Y00", 13, "MERIDIA.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Vaernima, "90C00Y00", 190, "VAERNIMA.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Nocturnal, "40C00Y00", 248, "NOCTURNA.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Mephala, "Z0C00Y00", 283, "MEPHALA.FLC"),
            new DaedraData((int) FactionFile.FactionIDs.Azura, "T0C00Y00", 81, "AZURA.FLC"),
        };

        protected Quest offeredQuest = null;

        protected DaedraData daedraToSummon;
        protected FactionFile.FactionData summonerFactionData;

        public DaggerfallQuestPopupWindow(IUserInterfaceManager uiManager, IUserInterfaceWindow previousWindow = null)
            : base(uiManager, previousWindow)
        {
        }

        public abstract MacroDataSource GetMacroDataSource();

        #region Service Handling: Quests

        protected abstract void GetQuest();
        
        protected virtual void ShowFailGetQuestMessage()
        {
            const int flavourMessageID = 600;

            // Display random flavour message such as "You're too late I gave the job to some spellsword"
            // This is a generic fallback hanlder, does not require quest data or MCP for this popup
            TextFile.Token[] tokens = DaggerfallUnity.Instance.TextProvider.GetRandomTokens(flavourMessageID);
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetTextTokens(tokens);
            messageBox.ClickAnywhereToClose = true;
            messageBox.AllowCancel = true;
            messageBox.ParentPanel.BackgroundColor = Color.clear;
            messageBox.Show();
        }

        // Show a popup such as accept/reject message close guild window
        protected virtual void ShowQuestPopupMessage(Quest quest, int id, bool exitOnClose = true)
        {
            // Get message resource
            Message message = quest.GetMessage(id);
            if (message == null)
                return;

            // Setup popup message
            TextFile.Token[] tokens = message.GetTextTokens();
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetTextTokens(tokens, offeredQuest.ExternalMCP);
            messageBox.ClickAnywhereToClose = true;
            messageBox.AllowCancel = true;
            messageBox.ParentPanel.BackgroundColor = Color.clear;

            // Exit menu on close if requested
            if (exitOnClose)
                messageBox.OnClose += QuestPopupMessage_OnClose;

            // Present popup message
            messageBox.Show();
        }

        protected virtual void OfferQuest_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                // Show accept message, add quest
                sender.CloseWindow();
                ShowQuestPopupMessage(offeredQuest, (int)QuestMachine.QuestMessages.AcceptQuest);
                QuestMachine.Instance.InstantiateQuest(offeredQuest);
            }
            else
            {
                // inform TalkManager so that it can remove the quest topics that have been added
                // (note by Nystul: I know it is a bit ugly that it is added in the first place at all, but didn't find a good way to do it differently -
                // may revisit this later)
                GameManager.Instance.TalkManager.RemoveQuestInfoTopicsForSpecificQuest(offeredQuest.UID);

                // remove quest rumors (rumor mill command) for this quest from talk manager
                GameManager.Instance.TalkManager.RemoveQuestRumorsFromRumorMill(offeredQuest.UID);

                // remove quest progress rumors for this quest from talk manager
                GameManager.Instance.TalkManager.RemoveQuestProgressRumorsFromRumorMill(offeredQuest.UID);

                // Show refuse message
                sender.CloseWindow();
                ShowQuestPopupMessage(offeredQuest, (int)QuestMachine.QuestMessages.RefuseQuest, false);
            }
        }

        protected virtual void QuestPopupMessage_OnClose()
        {
            CloseWindow();
        }

        #endregion

        #region Service Handling: Daedra Summoning

        protected void DaedraSummoningService(int npcFactionId)
        {
            if (!GameManager.Instance.PlayerEntity.FactionData.GetFactionData(npcFactionId, out summonerFactionData))
            {
                DaggerfallUnity.LogMessage("Error no faction data for NPC FactionId: " + npcFactionId);
                return;
            }
            // Select appropriate Daedra for summoning attempt.
            if (summonerFactionData.id == (int) FactionFile.FactionIDs.The_Glenmoril_Witches)
            {   // Always Hircine at Glenmoril witches.
                daedraToSummon = daedraData[0];
            }
            else if ((FactionFile.FactionTypes) summonerFactionData.type == FactionFile.FactionTypes.WitchesCoven)
            {   // Witches covens summon a random Daedra.
                daedraToSummon = daedraData[Random.Range(1, daedraData.Length)];
            }
            else
            {   // TODO - Sheogorath 5%/15% chance to replace days' daedra.
                int dayOfYear = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.DayOfYear;
                foreach (DaedraData dd in daedraData)
                {
                    if (dd.dayOfYear == dayOfYear)
                    {
                        daedraToSummon = dd;
                        break;
                    }
                }
            }
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(uiManager, uiManager.TopWindow);
            if (daedraToSummon.factionId == 0)
            {   // Display no summoning message.
                TextFile.Token[] tokens = DaggerfallUnity.Instance.TextProvider.GetRandomTokens(SummonNotToday);
                messageBox.SetTextTokens(tokens, this);
                messageBox.ClickAnywhereToClose = true;
            }
            else
            {   // Ask player if they really want to risk the summoning.
                messageBox.SetTextTokens(SummonAreYouSure, this);
                messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
                messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No);
                messageBox.OnButtonClick += ConfirmSummon_OnButtonClick;
            }
            uiManager.PushWindow(messageBox);
        }

        private void ConfirmSummon_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
            {
                PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                int summonCost = FormulaHelper.CalculateDaedraSummoningCost(summonerFactionData.rep);

                Debug.LogFormat("rep: {0}  cost: {1}", summonerFactionData.rep, summonCost);

                if (playerEntity.GetGoldAmount() >= summonCost)
                {
                    playerEntity.DeductGoldAmount(summonCost);

                    // TODO chance they will not show...

                    Debug.Log("Start the summoning! Offer the quest.");

                    // Check then record the summoning.
                    if (playerEntity.FactionData.GetFlag(daedraToSummon.factionId, FactionFile.Flags.Summoned))
                    {
                        DaggerfallUI.MessageBox(SummonBefore, this);  // TODO - play vid with this message
                    }
                    else
                    {
                        playerEntity.FactionData.SetFlag(daedraToSummon.factionId, FactionFile.Flags.Summoned);

                        // Offer the quest to player.
                        offeredQuest = GameManager.Instance.QuestListsManager.GetQuest(daedraToSummon.quest);
                        if (offeredQuest != null)
                        {
                            // Need to use DaggerfallDaedraSummoningWindow here...

                            DaggerfallMessageBox messageBox = QuestMachine.Instance.CreateMessagePrompt(offeredQuest, (int) QuestMachine.QuestMessages.QuestorOffer);
                            if (messageBox != null)
                            {
                                messageBox.OnButtonClick += OfferQuest_OnButtonClick;
                                messageBox.Show();
                            }

                            //Quest quest = QuestMachine.Instance.ParseQuest(daedraToSummon.quest);
                            //uiManager.PushWindow(new DaggerfallDaedraSummoning(uiManager, daedraToSummon));
                        }
                    }
                }
                else
                    DaggerfallUI.MessageBox(NotEnoughGoldId);
            }
        }

        #endregion

    }
}