import { NodeServiceClient } from "@/types/wrap/NodeServiceClient";
import log from "electron-log/renderer";

let _client: NodeServiceClient | undefined = undefined;

export const GetNodeService = (): NodeServiceClient => {
  if (_client === undefined) {
    Object.assign(console, log.functions);
    _client = new NodeServiceClient();
  }
  return _client;
};
