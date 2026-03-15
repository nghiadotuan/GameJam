
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class PopupWin : MonoBehaviour
    {
        public void OnClickBtnNextLevel()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.Equals("Level1")) SceneManager.LoadScene("Level2");
            if (scene.Equals("Level2")) SceneManager.LoadScene("Level3");
        }
    }
