using UnityEngine;
using UnityEngine.SceneManagement;

public class PopupLose : MonoBehaviour
    {
        public void OnClickBtnReplay()
        {
            var name = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(name);
        }
    }