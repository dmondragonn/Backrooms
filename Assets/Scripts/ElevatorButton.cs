using UnityEngine;

public class ElevatorButton : MonoBehaviour
{
    public TextMesh display;
    public string value;

    public void Press()
    {
        if (display != null && !string.IsNullOrEmpty(value))
        {
            display.text = value;
        }
    }

    private void OnMouseDown()
    {
        Press();
    }
}
