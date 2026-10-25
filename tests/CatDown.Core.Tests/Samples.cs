namespace CatDown.Core.Tests;

public static class Samples
{
    // One of each block kind CatDown handles.
    public const string AllKinds = """
        ---
        title: Setup
        ---

        # Getting started

        Install the **CLI** with the command below. Then run it.

        ```bash
        npm i -g foo
        ```

        - First item. Second sentence.
          - Nested item
        - [ ] Task item

        > Quoted text
        > over two lines.

        | Name | Note |
        | --- | --- |
        | `foo` | Does things. Many things. |

        <div>raw html</div>

        ***

        Last paragraph with a [link](https://example.com/a.b).

        """;
}
