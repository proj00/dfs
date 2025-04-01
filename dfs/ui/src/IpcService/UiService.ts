import { INodeService } from "./INodeService";
/*
export class UiService {
  private value = 0;
  //@ts-ignore
  private nodeService: INodeService;

  public constructor(nodeService: INodeService) {
    this.nodeService = nodeService;
  }

  public getValue() {
    return this.value;
  }

  public setValue(value: number) {
    this.value = value;
  }

  public async callExample() {
    return "hi";
  }

  public getCefSharpWrapper() {
    return {
      getValue: () => this.getValue(),
      setValue: (value: number) => this.setValue(value),
      callExample: async () => this.callExample(),
    };
  }
}
*/
export class UiService {
    private nodeService: INodeService;

    public constructor(nodeService: INodeService) {
        this.nodeService = nodeService;
    }

    public pickFile(): Promise<string> {
        return this.nodeService.PickObjectPath(false);
    }

    public pickFolder(): Promise<string> {
        return this.nodeService.PickObjectPath(true);
    }

    public importFile(path: string): Promise<void> {
        return this.nodeService.ImportObjectFromDisk(path, 1024);
    }

    public publishToTracker(hashes: string[], trackerUri: string): Promise<void> {
        return this.nodeService.PublishToTracker(hashes, trackerUri);
    }
}
