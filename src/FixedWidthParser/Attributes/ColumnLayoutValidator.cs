namespace FixedWidthParser.Attributes
{
    /// <summary>
    /// Valida o layout de colunas de um modelo na construção do parser/writer: rejeita
    /// <c>Start</c> negativo, <c>Length</c> não positivo e colunas sobrepostas — falhando cedo
    /// e com mensagem clara, em vez de produzir erros obscuros por linha em tempo de execução.
    /// </summary>
    public static class ColumnLayoutValidator
    {
        public static void Validate(List<(int Start, int Length, string Name)> columns, Type modelType)
        {
            foreach (var column in columns)
            {
                if (column.Start < 0)
                {
                    throw new InvalidOperationException(
                        $"Coluna \"{column.Name}\" em {modelType.Name} tem Start negativo ({column.Start}).");
                }
                if (column.Length < 1)
                {
                    throw new InvalidOperationException(
                        $"Coluna \"{column.Name}\" em {modelType.Name} tem Length inválido ({column.Length}); deve ser >= 1.");
                }
            }

            if (columns.Count < 2)
            {
                return;
            }

            // Detecta sobreposição ordenando por início e acompanhando o maior fim visto.
            columns.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));
            var farthest = columns[0];
            int maxEnd = farthest.Start + farthest.Length;
            for (int i = 1; i < columns.Count; i++)
            {
                var current = columns[i];
                if (current.Start < maxEnd)
                {
                    throw new InvalidOperationException(
                        $"Colunas sobrepostas em {modelType.Name}: \"{farthest.Name}\" [{farthest.Start}, {farthest.Start + farthest.Length}) " +
                        $"e \"{current.Name}\" [{current.Start}, {current.Start + current.Length}).");
                }
                int end = current.Start + current.Length;
                if (end > maxEnd)
                {
                    maxEnd = end;
                    farthest = current;
                }
            }
        }
    }
}
