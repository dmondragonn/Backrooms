using System.Collections;
using UnityEngine;

// Intro: player starts locked in an elevator. The floor counter rises 2 -> 3,
// it pauses, jolts, then plunges while the counter drops to -1 with a growing
// camera shake. The telescoping doors then retract and the maze is revealed.
// Motion is faked with camera shake (the world never actually moves).
public class ElevatorIntro : MonoBehaviour
{
    public SimplePlayer player;
    public Transform[] leftPanels, rightPanels;  // telescoping door panels per side
    public float[] panelSlides;                  // retract distance per panel index
    public TextMesh display;                     // floor number
    public float upTime = 4.5f;
    public float holdTime = 0.9f;
    public float fallTime = 3f;
    public float doorTime = 1.6f;

    IEnumerator Start()
    {
        yield return null;                 // let SimplePlayer build its camera first
        var camC = player.GetComponentInChildren<Camera>();
        if (camC == null) yield break;
        Transform cam = camC.transform;
        Vector3 eye = cam.localPosition;

        player.canMove = false;            // lock walking, but keep mouse-look free

        // ascend: counter 2 -> 3, faint hum/bob
        SetFloor(2);
        for (float t = 0; t < upTime; t += Time.deltaTime)
        {
            SetFloor(Mathf.Lerp(2f, 3f, t / upTime));
            cam.localPosition = eye + Vector3.up * Mathf.Sin(t * 9f) * 0.012f;
            yield return null;
        }
        SetFloor(3);

        cam.localPosition = eye;
        yield return new WaitForSeconds(holdTime); // stop at 3 for a beat

        // failure jolt: a sharp dip
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            cam.localPosition = eye + Vector3.down * 0.16f;
            yield return null;
        }

        // plunge: counter 3 -> -3 (accelerating), shake ramps up
        for (float t = 0; t < fallTime; t += Time.deltaTime)
        {
            float f = t / fallTime;
            SetFloor(Mathf.Lerp(3f, -3f, f * f));
            float k = Mathf.Lerp(0.03f, 0.14f, f);
            cam.localPosition = eye + (Vector3)(Random.insideUnitCircle * k);
            yield return null;
        }
        if (display) display.text = "???";   // off the bottom of the world
        cam.localPosition = eye;

        yield return new WaitForSeconds(0.5f); // arrival beat

        // telescoping doors retract: inner panels travel farther than outer ones
        var l0 = new Vector3[leftPanels.Length];
        var r0 = new Vector3[rightPanels.Length];
        for (int i = 0; i < leftPanels.Length; i++) { l0[i] = leftPanels[i].localPosition; r0[i] = rightPanels[i].localPosition; }

        for (float t = 0; t < doorTime; t += Time.deltaTime)
        {
            float f = Mathf.SmoothStep(0f, 1f, t / doorTime);
            for (int i = 0; i < leftPanels.Length; i++)
            {
                leftPanels[i].localPosition  = l0[i] + Vector3.left  * panelSlides[i] * f;
                rightPanels[i].localPosition = r0[i] + Vector3.right * panelSlides[i] * f;
            }
            yield return null;
        }

        player.canMove = true;             // hand walking back
    }

    void SetFloor(float v)
    {
        if (display) display.text = Mathf.RoundToInt(v).ToString();
    }
}
