import reactLogo from "./assets/react.svg";
import viteLogo from "/vite.svg";
import "./App.css";
import { GetNodeService, INodeService } from "./IpcService/INodeService";
import { useEffect, useState } from "react";
import { UiService } from "./IpcService/UiService";

let nodeService: INodeService;
let uiService: UiService;

function App() {
  const [hi, setHi] = useState("");
  const [count, setCount] = useState(0);

  useEffect(() => {
    const init = async () => {
      nodeService = await GetNodeService();
      uiService = new UiService(nodeService);
      uiService.setValue(1);

      await nodeService.RegisterUiService(uiService.getCefSharpWrapper());
      return await nodeService.Hi();
    };

    init().then((data) => setHi(data));
  }, []);

  return (
    <>
      <div>
        <a href="https://vite.dev" target="_blank">
          <img src={viteLogo} className="logo" alt="Vite logo" />
        </a>
        <a href="https://react.dev" target="_blank">
          <img src={reactLogo} className="logo react" alt="React logo" />
        </a>
      </div>
      <h1>Vite + React + {hi}</h1>
      <div className="card">
        <button
          onClick={async () => {
            const newCount = count + 1;
            setCount(newCount);
            uiService.setValue(newCount);
            setHi(await nodeService.Hi());
          }}
        >
          count is {count}
        </button>
        <p>
          Edit <code>src/App.tsx</code> and save to test HMR
        </p>
      </div>
      <p className="read-the-docs">
        Click on the Vite and React logos to learn more
      </p>
    </>
  );
}

export default App;
