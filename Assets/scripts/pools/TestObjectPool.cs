using UnityEngine;
using System.Collections.Generic;

public class TestObjectPool : MonoBehaviour
{
    // Вложенный класс для описания одного типа пула в инспекторе
    [System.Serializable]
    public class Pool
    {
        public string tag;      // Уникальное имя пула (например, "Пуля", "Взрыв")
        public GameObject prefab; // Префаб объекта, который будем переиспользовать
        public int size;          // Сколько объектов предзагрузить при старте
    }

    // Список всех пулов — заполняется в инспекторе Unity
    public List<Pool> pools;

    // Словарь: ключ — тег пула, значение — очередь объектов этого типа
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    void Awake()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        // Проходим по каждому описанию пула из инспектора
        foreach (Pool pool in pools)
        {
            // Создаём очередь для хранения свободных объектов
            Queue<GameObject> objectPool = new Queue<GameObject>();

            // Предзагружаем указанное количество объектов
            for (int i = 0; i < pool.size; i++)
            {
                // Создаём экземпляр префаба
                GameObject obj = Instantiate(pool.prefab);
                
                // Сразу деактивируем — пусть ждёт в "спящем" режиме
                obj.SetActive(false);
                
                // Прячем под этим менеджером для порядка в сцене
                obj.transform.SetParent(transform);
                
                // Добавляем в очередь свободных объектов
                objectPool.Enqueue(obj);
            }

            // Сохраняем очередь в словарь под ключом-тегом
            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    // Получить объект из пула и активировать его в указанной позиции
    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        // Проверяем, существует ли такой пул
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning("Пул с тегом «" + tag + "» не найден!");
            return null;
        }

        // Достаём первый свободный объект из очереди
        GameObject objectToSpawn = poolDictionary[tag].Dequeue();

        // Настраиваем его перед активацией
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.SetActive(true); // Включаем — объект "ожил"

        // ВАЖНО: объект НЕ возвращается в пул здесь!
        // Он вернётся позже — когда сам деактивируется (см. метод ReturnToPool)

        return objectToSpawn;
    }

    // Вернуть объект обратно в пул (вызывается из скрипта самого объекта)
    public void ReturnToPool(GameObject obj)
    {
        // Находим, к какому пулу принадлежит объект (по тегу префаба)
        string tag = GetPoolTagByPrefab(obj);

        if (poolDictionary.ContainsKey(tag))
        {
            obj.SetActive(false);          // Деактивируем
            obj.transform.SetParent(transform); // Возвращаем под менеджер для порядка
            poolDictionary[tag].Enqueue(obj);   // Кладём в конец очереди — снова свободен!
        }
        else
        {
            Debug.LogWarning("Не удалось вернуть объект в пул — неизвестный тег");
        }
    }

    // Вспомогательный метод: определяем тег пула по объекту
    private string GetPoolTagByPrefab(GameObject obj)
    {
        // Простой способ: ищем префаб в списке пулов и возвращаем его тег
        foreach (Pool pool in pools)
        {
            if (obj.GetComponent<IPooledObject>() != null && 
                obj.GetComponent<IPooledObject>().GetPoolTag() == pool.tag)
            {
                return pool.tag;
            }
        }
        return null;
    }
}

// Интерфейс для объектов из пула — помогает им самим себя возвращать
public interface IPooledObject
{
    string GetPoolTag(); // Каждый объект знает, к какому пулу принадлежит
}