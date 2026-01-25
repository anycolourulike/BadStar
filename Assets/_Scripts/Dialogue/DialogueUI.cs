using System.Collections;
using System.Collections.Generic;
using Rambler.Dialogue;
using UnityEngine.UI;
using UnityEngine;
using TMPro;


namespace Rambler.UI
{
    public class DialogueUI : MonoBehaviour
    {                
        [SerializeField] Button quitButton;       
        [SerializeField] Button nextButton;        
        [SerializeField] Transform choiceRoot;
        [SerializeField] GameObject AIResponse;
        [SerializeField] GameObject choicePrefab;
        [SerializeField] TextMeshProUGUI AIText;
        [SerializeField] TextMeshProUGUI conversantName;
        [SerializeField] PlayerConversant playerConversant;       

        public void InitDialogue()
        {
            playerConversant.onConversationUpdated += UpdateUI;
            nextButton.onClick.AddListener(() => playerConversant.Next());
            quitButton.onClick.AddListener(() => playerConversant.Quit());
            UpdateUI();
        }

        void UpdateUI()
        {
            gameObject.SetActive(playerConversant.IsActive());
            if (!playerConversant.IsActive())
            {
                return;
            }
            conversantName.text = playerConversant.GetCurrentConversantName();
            AIResponse.SetActive(!playerConversant.IsChoosing());
            choiceRoot.gameObject.SetActive(playerConversant.IsChoosing());
            if (playerConversant.IsChoosing())
            {
                BuildChoiceList();
            }
            else
            {
                AIText.text = playerConversant.GetText();
                nextButton.gameObject.SetActive(playerConversant.HasNext());
            }
        }

        private void BuildChoiceList()
        {
            foreach (Transform item in choiceRoot)
            {
                Destroy(item.gameObject);
            }
            foreach (DialogueNode choice in playerConversant.GetChoices())
            {
                GameObject choiceInstance = Instantiate(choicePrefab, choiceRoot);
                var textComp = choiceInstance.GetComponentInChildren<TextMeshProUGUI>();
                textComp.text = choice.GetText();
                Button button = choiceInstance.GetComponentInChildren<Button>();
                button.onClick.AddListener(() =>{playerConversant.SelectChoice(choice);});
            }
        }
    }
}
