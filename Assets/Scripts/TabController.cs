using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

public class TabController : MonoBehaviour
{
        public Image[] tabImages; // Array to hold references to the tab images
        public GameObject[] pages; // Array to hold references to the page GameObjects
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
         ActivateTab(0); // Activate the first tab by default
    }
    
    public void ActivateTab(int index)
    {
        // Loop through all tabs and pages
        for (int i = 0; i < tabImages.Length; i++)
        {
            // Set the active state of the page based on the index
            pages[i].SetActive(i == index);
            
            // Change the appearance of the tab images here
            if (i == index)
            {
                tabImages[i].color = Color.white; // Active tab color
            }
            else
            {
                tabImages[i].color = Color.gray; // Inactive tab color
            }
        }
    }

}
