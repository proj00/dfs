import { Drive } from "./components/drive";
import { getContents } from "./lib/getData";

function App() {
  return <Drive contentsPromise={getContents()} />;
}

export default App;
