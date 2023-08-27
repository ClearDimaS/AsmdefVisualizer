using System;
using UnityEditor;
using UnityEngine;

public class Connection
{
    public ConnectionPoint inPoint;
    public ConnectionPoint outPoint;
    public Action<Connection> OnClickRemoveConnection;

    private Color _deselectedColor = new Color(1, 1, 1, 0.2f);
    private Color _selectedColor = new Color(1, 0, 0, 0.4f);

    private float _deselectedWidth = 2f;
    private float _selectedWidth = 3f;

    public Connection(ConnectionPoint inPoint, ConnectionPoint outPoint, Action<Connection> OnClickRemoveConnection)
    {
        this.inPoint = inPoint;
        this.outPoint = outPoint;
        this.OnClickRemoveConnection = OnClickRemoveConnection;
    }

    public virtual void Draw()
    {
        var isSelected = outPoint.node.isSelected || inPoint.node.isSelected;
        Handles.DrawBezier(
            inPoint.rect.center,
            outPoint.rect.center,
            inPoint.rect.center + Vector2.left * 50f,
            outPoint.rect.center - Vector2.left * 50f,
            isSelected ? _selectedColor : _deselectedColor,
            null,
            isSelected ? _selectedWidth : _deselectedWidth
        );

        /*if (Handles.Button((inPoint.rect.center + outPoint.rect.center) * 0.5f, Quaternion.identity, 4, 8, Handles.RectangleHandleCap))
        {
            if (OnClickRemoveConnection != null)
            {
                OnClickRemoveConnection(this);
            }
        }*/
    }
}