using UnityEngine;

namespace PhysSound.Samples.Scripts
{
    public class PhysSoundDemo : MonoBehaviour
    {
        public GameObject[] Objects;
        public float RandomForce;

        private void Start()
        {
            foreach (GameObject g in Objects)
                g.GetComponent<Rigidbody>().maxAngularVelocity = 1000;

            Drop(-1);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
                Drop(-1);

            if (Input.GetKeyDown(KeyCode.Alpha1))
                Drop(0);

            if (Input.GetKeyDown(KeyCode.Alpha2))
                Drop(1);

            if (Input.GetKeyDown(KeyCode.Alpha3))
                Drop(2);

            if (Input.GetKeyDown(KeyCode.Alpha4))
                Drop(3);

            if (Input.GetKeyDown(KeyCode.Alpha5))
                Drop(4);
        }

        private void Drop(int index)
        {
            for (int i = 0; i < Objects.Length; i++)
            {
                if (index == -1 || i == index)
                {
                    GameObject g = Objects[i];
                    g.transform.position = transform.position + Random.insideUnitSphere * 1.5f;
                    g.GetComponent<Rigidbody>().velocity = new Vector3(Random.Range(-RandomForce, RandomForce),
                        Random.Range(-RandomForce, RandomForce),
                        0);

                    g.GetComponent<Rigidbody>().angularVelocity = Random.insideUnitSphere * RandomForce;
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 50));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Box(Application.loadedLevelName);
            GUILayout.Box("Press 'Q' to drop objects.");
            GUILayout.Box("Press '1', '2', '3', '4', or '5' to drop specific objects.");
            //GUILayout.Box("Current Object: " + Target.name);
            //GUILayout.Box("'1' '2' '3' or '4' to load different scenes.");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}