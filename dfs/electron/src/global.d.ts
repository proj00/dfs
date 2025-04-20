export {};
import type { UiClient } from "./types/rpc/uiservice.client";
import type { UnaryCall } from "@protobuf-ts/runtime-rpc";

export type RpcMethodNames = {
  [K in keyof UiClient]: UiClient[K] extends (...args: any[]) => any
    ? K
    : never;
}[keyof UiClient];

export type RpcRequest<M extends RpcMethodNames> = Parameters<UiClient[M]>[0];

export type CallType<M extends RpcMethodNames> = ReturnType<UiClient[M]>;

export type RpcResponse<M extends UiRpcMethodNames> =
  ReturnType<UiClient[M]> extends UnaryCall<any, infer R> ? R : never;

declare global {
  interface Window {
    electronAPI: {
      selectFile: () => Promise<string | null>;
      selectFolder: () => Promise<string | null>;
      writeClipboard: (text: string) => void;
      readClipboard: () => string;
      onAppQuit: (callback: () => void) => void;
      confirmQuit: () => void;
      callGrpc<_method extends RpcMethodNames>(
        method: _method,
        arg: RpcRequest<_method>,
      ): Promise<RpcResponse<_method>>;
    };
  }
}
