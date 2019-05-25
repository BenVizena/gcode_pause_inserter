using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UI;

public class GcodeManipulator : MonoBehaviour
{
    private string[] lines;
    private int layer_count = -1;
    private double layer_height = -1;
    private int gcode_line_number_to_pause_at = 0;
    private Shape preview_shape = new Shape(-1);
    private ArrayList lr_list = new ArrayList();
    private int pause_location = 0;
    private string path_to_gcode = "";


    private double LINE_THRESHOLD = 2; // minimum mm before considering two points a line.

    public Button load_gcode_button;
    public Button insert_pause_button;
    public Button preview_pause_location_button;
    public InputField gcode_file_name_inputfield;
    public InputField pause_duration_inputfield;
    public InputField pause_location_inputfield;
    public GameObject plane;

    // Start is called before the first frame update
    void Start()
    {
        load_gcode_button.onClick.AddListener(Load_Gcode_Pressed);
        insert_pause_button.onClick.AddListener(Insert_Pause_Pressed);
        preview_pause_location_button.onClick.AddListener(Preview_Pause_Location_Pressed);
    }

    // Update is called once per frame
    void Update()
    {
    }

    void Load_Gcode_Pressed()
    {
        path_to_gcode = gcode_file_name_inputfield.text;
        if (!File.Exists(@path_to_gcode))
        {
            Debug.Log(path_to_gcode + " does not exist!");
        }
        else
        {
            lines = System.IO.File.ReadAllLines(@path_to_gcode);
        }
    }

    void Insert_Pause_Pressed()
    {
        if (Int32.TryParse(pause_duration_inputfield.text, out int pause_duration))
        {
            string destination_file = path_to_gcode.Split('.')[0] + "_pause_added.gcode";

            if (File.Exists(@destination_file))
            {
                File.Delete(@destination_file);
            }

            string[] resulting_lines = new string[lines.Length + 2];

            int new_file_line = 0;
            int old_file_line = 0;

            foreach (string line in lines)
            {
                if (old_file_line == gcode_line_number_to_pause_at)
                {
                    resulting_lines[new_file_line] = "G0 X10.0 Y10.0";
                    new_file_line++;
                    resulting_lines[new_file_line] = "G4 P" + (pause_duration * 1000);
                    new_file_line ++;
                }

                resulting_lines[new_file_line] = lines[old_file_line];
                new_file_line++;
                old_file_line++;
            }
            using (StreamWriter writer = new StreamWriter(destination_file))
            {
                for (int current_gcode_line = 0; current_gcode_line < resulting_lines.Length; current_gcode_line++)
                {
                    writer.WriteLine(resulting_lines[current_gcode_line]);
                }
            }
        }
    }

    void Preview_Pause_Location_Pressed()
    {
        if (Int32.TryParse(pause_location_inputfield.text, out pause_location))
        {
            preview_shape = Create_Up_To_Line_X(pause_location);
            Draw_Shape();
        }
        else
        {
            Debug.Log("INVALID VALUE!");
        }
    }

    void reset_lr_list()
    {
        foreach (LineRenderer lr in lr_list)
        {
            lr.positionCount = 0;
            if (lr.gameObject != null)
            {
                Destroy(lr.gameObject);
            }
        }
        lr_list.Clear();
    }

    void Draw_Shape()
    {
        float z_height = -1f;

        reset_lr_list();

        Color color = new Color(0, 0, 0, 1);

        foreach (Layer layer in preview_shape.get_layer_list())
        {
            ArrayList vertex_list = new ArrayList();
            z_height = (float)layer.z_height;
            float layer_height_float = (float)layer_height;
            var random = new UnityEngine.Random();

            Vector3 previous_point = new Vector3(-1, -1, -1);
            Vector3 this_point1 = new Vector3(-1, -1, -1);
            Vector3 this_point2 = new Vector3(-1, -1, -1);

            foreach(Line line in layer.get_line_list())
            {
                this_point1.z = line.p1.x;
                this_point1.x = line.p1.y;
                this_point1.y = z_height;

                this_point2.z = line.p2.x;
                this_point2.x = line.p2.y;
                this_point2.y = z_height;

                if (this_point1 != previous_point)
                {
                    vertex_list.Add(this_point1);
                }

                vertex_list.Add(this_point2);
                previous_point = this_point2;
            }
            LineRenderer lineRenderer = new GameObject().AddComponent<LineRenderer>();
            lineRenderer.gameObject.transform.parent = plane.transform;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.gameObject.GetComponent<Renderer>().material.color = color;
            lineRenderer.widthMultiplier = .2f;
            lineRenderer.positionCount = vertex_list.Count;
            lineRenderer.SetPositions((Vector3[])vertex_list.ToArray(typeof(Vector3)));
            lr_list.Add(lineRenderer);

            color.b = UnityEngine.Random.value;
            color.r = UnityEngine.Random.value;
            color.g = UnityEngine.Random.value;
        }
    }

    Shape Create_Up_To_Line_X(int x)
    {
        Vector2 current_point = new Vector2(0,0);
        bool making_lines = false;

        Layer current_layer = new Layer(1);
        Shape shape = new Shape(1);

        foreach (string line in lines)
        {
            gcode_line_number_to_pause_at++;
            if (layer_count == -1)
            {
                if (line.Contains("LAYER_COUNT:"))
                {
                    if (Int32.TryParse(line.Split(':')[1], out layer_count) == false)
                    {
                        Debug.Log("Failed to get layer_count!  Line is: " + line);
                    }
                }
            }
            else if (line.Contains("Layer height:"))
            {
                if (Double.TryParse(line.Split(':')[1].Trim(), out layer_height) == false)
                {
                    Debug.Log("Failed to get layer height! line: " + line);
                }
            }
            else if (line.Contains("G1") && line.Contains("X") && line.Contains("Y"))
            {
                double x_var;
                double y_var;

                if (!Double.TryParse(line.Split('X')[1].Split(' ')[0], out x_var))
                {
                    Debug.Log("couldn't get x val for line: " + line);
                }
                if (!Double.TryParse(line.Split('Y')[1].Split(' ')[0], out y_var))
                {
                    Debug.Log("couldn't get y val for line: " + line);
                }

                if (!making_lines)
                {
                    making_lines = true;
                    current_point.x = (float)x_var;
                    current_point.y = (float)y_var;
                }
                else
                {
                    Vector2 second_point = new Vector2((float)x_var, (float)y_var);

                    if (get_distance_between_points(current_point, second_point) >= LINE_THRESHOLD)
                    {
                        Line l = new Line(current_point, second_point);
                        current_layer.add_line(l);
                        current_point = second_point; // this way we can start making a new line off of this point. don't need to set making_lines to false.
                    }
                }
            }
            else if (line.Contains("LAYER:"))
            {
                int current_layer_number = -1;
                if (!Int32.TryParse(line.Split(':')[1], out current_layer_number))
                {
                    Debug.Log("couldn't get current_layer_number from line: " + line);
                }
                else if (current_layer_number >= 1)
                {
                    shape.add_layer(current_layer);
                    current_layer = new Layer(1);
                }

                if (current_layer_number > x)
                {
                    break;
                }
            }
            else if (line.Contains("G0") && line.Contains("X") && line.Contains("Y") && line.Contains("Z"))
            {
                if (Double.TryParse(line.Split('Z')[1], out Double layer_height))
                {
                    current_layer.z_height = layer_height;
                }
            }
        }
        return shape;
    }

    private struct Shape
    {
        private ArrayList layer_list;

        public Shape(int i)
        {
            layer_list = new ArrayList();
        }

        public void add_layer(Layer l)
        {
            layer_list.Add(l);
        }

        public ArrayList get_layer_list()
        {
            return layer_list;
        }

        // for debugging
        public void print_shape()
        {
            foreach (Layer layer in layer_list)
            {
                Debug.Log("this layer has this many lines: " + layer.get_line_list().Count + " and has height: " + layer.z_height);
            }
        }
    }

    private struct Layer
    {
        private ArrayList line_list;
        public Double z_height;

        public Layer(int i)
        {
            line_list = new ArrayList();
            z_height = -5;
        }

        public void add_line(Line l)
        {
            line_list.Add(l);
        }

        public ArrayList get_line_list()
        {
            return line_list;
        }
    }

    private struct Line
    {
        public Vector2 p1, p2;
        public float x_diff;
        public float y_diff;
        public float slope;
        public double magnitude;

        public Line(Vector2 i1, Vector2 i2)
        {
            p1 = i1;
            p2 = i2;

            x_diff = p2.x - p1.x;
            y_diff = p2.y - p1.y;

            if (x_diff == 0)
            {
                slope = float.PositiveInfinity;
            }
            else
            {
                slope = (float)((float)y_diff / x_diff);
            }

            magnitude = get_distance_between_points(p1, p2);
        }
    }

    private static double get_distance_between_points(Vector2 p1, Vector2 p2)
    {
        return Math.Sqrt(Math.Pow(p2.x - p1.x, 2) + Math.Pow(p2.y - p1.y, 2));
    }

    private double get_angle_between_lines(Line l1, Line l2)
    {
        double angle1 = Math.Atan((double)l1.y_diff / l1.x_diff) * 57.2958; //  * 360 / 2 / pi
        double angle2 = Math.Atan((double)l2.y_diff / l2.x_diff) * 57.2958; //  * 360 / 2 / pi

        return Math.Abs(angle1 - angle2);
    }
}
