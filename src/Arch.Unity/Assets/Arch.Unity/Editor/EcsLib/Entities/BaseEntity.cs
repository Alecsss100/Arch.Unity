using System.Collections.Generic;

class BaseEntity
{
    public virtual List<object> GetComponents() => new List<object>();
}