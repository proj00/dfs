import { INodeService } from "./INodeService";

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
