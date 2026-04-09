using System;
using System.Collections.Generic;

/// <summary>
/// Аспект это всего лишь группа компонентов. 
/// Цель аспекта убрать монотонное добавление компонентов объединив их в понятную группу/аспект
/// </summary>
public interface IAspect
{
    public IEnumerable<object> GetComponentTypeList();
}