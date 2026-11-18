using System.Collections;
using System.Collections.Generic;
using Rambler.Quests;
using UnityEngine;

namespace Rambler.Quests
{      
    public class QuestListUI : MonoBehaviour
    {
        [SerializeField] QuestItemUI questPrefab;
        QuestList questList;
    
        void Start()
        {
            questList = QuestList.Find();
            if (questList == null) return;
            questList.onUpdate += Redraw;
            Redraw();
        }

        private void Redraw()
        {
            foreach (Transform item in transform)
            {
               Destroy(item.gameObject);
            }
            foreach (QuestStatus status in questList.GetStatuses())
            {
               QuestItemUI uiInstance = Instantiate<QuestItemUI>(questPrefab, transform);
               uiInstance.Setup(status);
           }
        }   
    }
}